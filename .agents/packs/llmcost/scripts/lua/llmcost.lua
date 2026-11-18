--[[
name: llmcost
title: LLM cost tracker
description: Prices the token usage of the current chat and its agentic tasks and shows the running total, split by token type, in a floating overlay.
namespace: llmcost
manuals: |
  --- llmcost - token spend of the current chat

  Every assistant message and every agentic task launch of this chat is recorded into the
  chat's ledger, priced per model and rendered into an overlay window over the message area.
  The ledger lives in store.chat, so it follows the chat and survives a restart. The window is
  re-appended under one replacement key on every load, so a reload replaces it - keeping its
  place, size and collapsed state - instead of opening a second one.

  Only tokens are stored: the price is computed at render time from the model's rate table and
  the record's own timestamp, so the overlay can break the total down by token type (cache hit,
  cache miss, output) and an updated price table re-prices the whole history.

  FUNCTIONS:

  --- llmcost.render()
    Ensures the overlay exists and refreshes it from the ledger.

  --- llmcost.record_message(payload)
    Records one message.assistant.stop payload. No-op without usage.

  --- llmcost.record_task(payload)
    Records one task.stop payload. Uses new_usage, never the cumulative usage, so a
    continuation is billed once.

  --- llmcost.stats([from]) -> table
    The aggregation behind the overlay: breakdown (per token type), models (per model),
    total, hit, miss, out, messages, tasks, unpriced. `from` is an optional unix timestamp;
    entries older than it are left out, which is how the llm-cost tool reports a window.

  --- llmcost.fill(view_model)
    Writes the aggregation into an existing dynamic view model.

  PRICES:
    Fixed from https://api-docs.deepseek.com/quick_start/pricing (2026-11-18), USD per 1M
    tokens. Off-peak is half of the peak rate; peak is 01:00-04:00 and 06:00-10:00 UTC,
    Monday to Friday. Chinese public holidays are not modelled - a holiday inside a peak
    window is billed here at the peak rate.
]]

-- Prices per 1M tokens, USD. Peak and off-peak differ by exactly a factor of two.
local prices = {
	["deepseek-flash"] = {
		hit = { off = 0.003, peak = 0.006 },
		miss = { off = 0.15, peak = 0.30 },
		out = { off = 0.60, peak = 1.20 },
	},
	["deepseek-v4-pro"] = {
		hit = { off = 0.022, peak = 0.044 },
		miss = { off = 0.66, peak = 1.32 },
		out = { off = 1.98, peak = 3.96 },
	},
}

-- The pricing page keeps billing these retired names under the Flash rate.
local aliases = {
	["deepseek-v4-flash"] = "deepseek-flash",
	["deepseek-v4-flash-vision-exp"] = "deepseek-flash",
}

-- Window geometry as fractions of the messages area, so it keeps its place on resize.
local geometry = { left = 0.68, top = 0.04, width = 0.31, height = 0.46 }

local xaml = [==[
<StackPanel xmlns="https://github.com/avaloniaui" Spacing="8" Margin="12">
	<StackPanel Spacing="2">
		<TextBlock Text="{Binding total}" FontSize="20" FontWeight="Bold"/>
		<TextBlock Text="{Binding calls}" FontSize="11" Opacity="0.65"/>
	</StackPanel>
	<Border Height="1" Background="#33808080"/>
	<TextBlock Text="by token type" FontSize="10" Opacity="0.5"/>
	<ItemsControl ItemsSource="{Binding breakdown}">
		<ItemsControl.ItemTemplate>
			<DataTemplate>
				<Grid ColumnDefinitions="*,Auto,Auto,Auto" Margin="0,2">
					<TextBlock Grid.Column="0" Text="{Binding name}" FontSize="12"
						VerticalAlignment="Center"/>
					<TextBlock Grid.Column="1" Text="{Binding detail}" FontSize="11" Opacity="0.6"
						Margin="0,0,10,0" VerticalAlignment="Center"/>
					<TextBlock Grid.Column="2" Text="{Binding share}" FontSize="11" Opacity="0.8"
						Margin="0,0,10,0" VerticalAlignment="Center"/>
					<TextBlock Grid.Column="3" Text="{Binding cost}" FontSize="12"
						VerticalAlignment="Center"/>
				</Grid>
			</DataTemplate>
		</ItemsControl.ItemTemplate>
	</ItemsControl>
	<Border Height="1" Background="#33808080"/>
	<TextBlock Text="by model" FontSize="10" Opacity="0.5"/>
	<ItemsControl ItemsSource="{Binding models}">
		<ItemsControl.ItemTemplate>
			<DataTemplate>
				<Grid ColumnDefinitions="*,Auto" Margin="0,2">
					<StackPanel Grid.Column="0" Spacing="0">
						<TextBlock Text="{Binding name}" FontSize="12"/>
						<TextBlock Text="{Binding detail}" FontSize="10" Opacity="0.6"
							TextWrapping="Wrap"/>
					</StackPanel>
					<TextBlock Grid.Column="1" Text="{Binding cost}" FontSize="12"
						Margin="8,0,0,0" VerticalAlignment="Center"/>
				</Grid>
			</DataTemplate>
		</ItemsControl.ItemTemplate>
	</ItemsControl>
	<TextBlock Text="{Binding note}" FontSize="10" Opacity="0.6" TextWrapping="Wrap"
		IsVisible="{Binding has_note}"/>
</StackPanel>
]==]

local ledger = store.chat
local rowsKey = "rows"
local seenKey = "seen"

-- Peak: 01:00-04:00 and 06:00-10:00 UTC on weekdays. Both bounds are computed from the epoch,
-- so the result never depends on the machine's time zone.
local function isPeak(timestamp)
	local days = math.floor(timestamp / 86400)
	local weekday = (days + 4) % 7
	local hour = math.floor((timestamp - days * 86400) / 3600)
	if weekday == 0 or weekday == 6 then return false end
	return (hour >= 1 and hour < 4) or (hour >= 6 and hour < 10)
end

-- Prices are keyed by the bare model name: the provider name is a user-defined label in dASS.
local function modelOf(fullName)
	if fullName == nil or fullName == "" then return "unknown" end
	local parsed = dass.model.parse(fullName)
	local name = parsed and parsed.model or fullName
	return aliases[name] or name
end

-- Cached tokens, when reported, ARE the input: hit + miss is the input total, so input_tokens
-- is never added on top of them. Without cache metadata the whole input counts as a miss.
-- A provider that reports no split at all (or reports it as zero) is billed as a miss, never
-- as a free request: an expired cache has to land somewhere, and the miss rate is where.
local function tokensOf(usage)
	local input = usage.input_tokens or 0
	local hit = usage.input_cache_hit_tokens
	local miss = usage.input_cache_miss_tokens
	if hit == nil and miss == nil then
		return 0, input, usage.output_tokens or 0
	end

	hit = hit or 0
	miss = miss or math.max(input - hit, 0)
	if miss == 0 and input > hit then
		miss = input - hit
	end

	-- Cache writes reported outside the miss bucket (Anthropic-style) are billed as misses.
	local create = usage.cache_create_tokens or 0
	if create > 0 and input > hit + miss then
		miss = miss + create
	end

	return hit, miss, usage.output_tokens or 0
end

-- The three components are priced separately, which is the whole point of the breakdown:
-- a cache hit is two orders of magnitude cheaper than a miss.
local function costsOf(row)
	local rates = prices[row.model]
	if rates == nil then return nil end
	local peak = isPeak(row.at)
	local function rate(kind)
		local pair = rates[kind]
		return peak and pair.peak or pair.off
	end
	return (row.hit or 0) * rate("hit") / 1000000,
		(row.miss or 0) * rate("miss") / 1000000,
		(row.out or 0) * rate("out") / 1000000
end

local function fmtTokens(value)
	value = value or 0
	if value >= 1000000 then return string.format("%.2fM", value / 1000000) end
	if value >= 1000 then return string.format("%.1fk", value / 1000) end
	return tostring(value)
end

local function fmtCost(value)
	if value <= 0 then return "$0" end
	if value >= 1 then return string.format("$%.2f", value) end
	if value >= 0.01 then return string.format("$%.4f", value) end
	return string.format("$%.6f", value)
end

-- A share of the priced total: unpriced entries contribute tokens but no money, so the
-- percentages always add up to 100% of what could actually be billed.
local function pct(part, whole)
	if whole <= 0 then return "n/a" end
	return string.format("%.1f%%", part / whole * 100)
end

-- One entry per message or task launch, deduplicated by id: a hook registered twice (a pack
-- file and a runtime registration) must not bill the same request twice.
local function record(id, model, usage, who, kind, status, at)
	local seen = ledger.get_or_add(seenKey, {})
	if seen[id] then return end
	seen[id] = true

	local hit, miss, out = tokensOf(usage)
	local rows = ledger.get_or_add(rowsKey, {})
	rows[#rows + 1] = {
		id = id,
		kind = kind,
		model = model,
		who = who,
		status = status,
		hit = hit,
		miss = miss,
		out = out,
		at = at,
	}
	ledger.save()
end

-- An optional lower bound (unix seconds) narrows the report to a window; the overlay always
-- calls it without one.
local function aggregate(from)
	local source = ledger.get_or_add(rowsKey, {})
	local rows = source
	if from ~= nil then
		rows = {}
		for _, row in ipairs(source) do
			if (row.at or 0) >= from then
				rows[#rows + 1] = row
			end
		end
	end
	local groups = {}
	local order = {}
	local total, unpriced = 0, 0
	local hitSum, missSum, outSum = 0, 0, 0
	local costHit, costMiss, costOut = 0, 0, 0
	local messages, tasks = 0, 0

	for _, row in ipairs(rows) do
		local key = row.model or "unknown"
		local group = groups[key]
		if group == nil then
			group = { name = key, cost = 0, priced = true, count = 0 }
			groups[key] = group
			order[#order + 1] = group
		end

		group.count = group.count + 1
		hitSum = hitSum + (row.hit or 0)
		missSum = missSum + (row.miss or 0)
		outSum = outSum + (row.out or 0)
		if row.kind == "task" then
			tasks = tasks + 1
		else
			messages = messages + 1
		end

		local hitCost, missCost, outCost = costsOf(row)
		if hitCost == nil then
			group.priced = false
			unpriced = unpriced + 1
		else
			group.cost = group.cost + hitCost + missCost + outCost
			total = total + hitCost + missCost + outCost
			costHit = costHit + hitCost
			costMiss = costMiss + missCost
			costOut = costOut + outCost
		end
	end

	table.sort(order, function(a, b) return a.cost > b.cost end)

	local breakdown = {
		{ name = "Cache hit", detail = fmtTokens(hitSum), share = pct(costHit, total), cost = fmtCost(costHit) },
		{ name = "Cache miss", detail = fmtTokens(missSum), share = pct(costMiss, total), cost = fmtCost(costMiss) },
		{ name = "Output", detail = fmtTokens(outSum), share = pct(costOut, total), cost = fmtCost(costOut) },
	}

	local models = {}
	for _, group in ipairs(order) do
		models[#models + 1] = {
			name = group.name,
			detail = string.format("%d calls", group.count),
			cost = group.priced and fmtCost(group.cost) or "no price",
		}
	end

	return {
		breakdown = breakdown,
		models = models,
		total = total,
		hit = hitSum,
		miss = missSum,
		out = outSum,
		messages = messages,
		tasks = tasks,
		unpriced = unpriced,
	}
end

-- A revived command has no upvalues but resolves globals, so it reaches the current namespace.
local function put(vm, name, value)
	if vm.set ~= nil then
		vm:set(name, value)
	else
		vm[name] = value
	end
end

local function fill(vm)
	local stats = aggregate()
	put(vm, "total", fmtCost(stats.total))
	put(vm, "calls", string.format("%d messages · %d task launches", stats.messages, stats.tasks))
	put(vm, "breakdown", stats.breakdown)
	put(vm, "models", stats.models)
	put(vm, "has_note", stats.unpriced > 0)
	if stats.unpriced > 0 then
		put(vm, "note", string.format("%d entries without a price: the model is not in the price table", stats.unpriced))
	else
		put(vm, "note", "")
	end
end

local function newVm()
	local vm = {
		total = "$0",
		calls = "",
		breakdown = {},
		models = {},
		note = "",
		has_note = false,
	}
	return vm
end

-- The window is one chat additional-data item under a fixed replacement key: re-appending on
-- every load replaces it (the place, the size and the collapsed state are inherited from the
-- previous item) instead of opening a second window. No handle has to be kept anywhere.
local liveVm = nil

local function createOverlay()
	local vm = newVm()
	local control = dass.ui.create_control(xaml, vm)
	dass.chat.append_data(control, {
		surface = "overlay",
		title = "Chat spend",
		replacement_key = "llmcost",
		closeable = false,
		left = geometry.left,
		top = geometry.top,
		width = geometry.width,
		height = geometry.height,
	})
	liveVm = vm   -- only after a successful append: a failed load retries on the next event
	fill(vm)
end

llmcost.fill = fill
llmcost.stats = aggregate

llmcost.render = function()
	if liveVm == nil then
		createOverlay()
		return
	end
	fill(liveVm)
end

llmcost.record_message = function(payload)
	if payload == nil or payload.usage == nil then return end
	local at = time.now()
	local id = payload.message_id
	if id == nil then
		id = string.format("%s:%s:%s", tostring(payload.index), tostring(payload.cycle), tostring(at))
	end
	record("m:" .. tostring(id), modelOf(payload.usage.model), payload.usage,
		payload.agent or "agent", "message", payload.status, at)
	llmcost.render()
end

llmcost.record_task = function(payload)
	if payload == nil or payload.new_usage == nil then return end
	local at = time.now()
	record("t:" .. tostring(payload.task_id) .. ":" .. tostring(payload.started_at),
		modelOf(payload.model), payload.new_usage,
		payload.task_name or payload.source_id or "task", "task", payload.status, at)
	llmcost.render()
end

-- The overlay is created when the script loads; a chat without a Lua state yet gets it on the
-- first hook or the first tool call. A failed append must not kill the whole namespace.
local ok, err = pcall(llmcost.render)
if not ok then
	dass.log.warning("llmcost: the overlay could not be created at load: {Error}", tostring(err))
end
