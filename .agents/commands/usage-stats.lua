--[[
name: usage-stats
title: Chat usage stats
description: Folds the token usage of this chat into one card - input, output, prompt-cache reuse, time to first token and generation speed, plus a per-model breakdown. Counts the active branch only and appends the same numbers as text, so the model can read them. models=false hides the breakdown.
category: Chat
generate: false
model-facing: neutral
argument-schema:
  keyed:
    models:
      description: Show the per-model breakdown.
      type: boolean
      default: "true"
]]

-- The chat stores one usage record per assistant message; everything here is a fold over
-- those records. No price table is consulted on purpose: a token count is a fact the chat
-- reports, a price is not.

local showModels = true
local keyed = (command_args or {}).keyed or {}
if keyed.models ~= nil then
	showModels = keyed.models == true
end

local function group(value)
	local digits = tostring(math.floor((value or 0) + 0.5))
	local parts = {}
	for i = #digits, 1, -3 do
		table.insert(parts, 1, digits:sub(math.max(1, i - 2), i))
	end
	return table.concat(parts, ",")
end

local function compact(value)
	local n = value or 0
	if n >= 1e9 then
		return string.format("%.2fB", n / 1e9)
	end
	if n >= 1e6 then
		return string.format("%.2fM", n / 1e6)
	end
	if n >= 1e4 then
		return string.format("%.1fK", n / 1e3)
	end
	if n >= 1e3 then
		return string.format("%.2fK", n / 1e3)
	end
	return tostring(math.floor(n + 0.5))
end

local function pct(value)
	return string.format("%.1f%%", value * 100)
end

local function duration(seconds)
	if seconds >= 3600 then
		return string.format("%.1f h", seconds / 3600)
	end
	if seconds >= 60 then
		return string.format("%.1f min", seconds / 60)
	end
	return string.format("%.1f s", seconds)
end

local function truncate(text, limit)
	if #text <= limit then
		return text
	end
	return text:sub(1, limit - 1) .. "…"
end

-- A provider that reports no cache split leaves the miss counter empty, so the rest of the
-- input is the uncached share. Returns the uncached token count.
local function uncached_of(cached, missed, written, input)
	local total = missed + written
	if total == 0 and input > cached then
		total = input - cached
	end
	return total
end

local function gather()
	local s = {
		total = 0, user = 0, assistant = 0, other = 0,
		turns = 0, input = 0, cached = 0, missed = 0, written = 0, output = 0,
		ttft = 0, gen = 0, models = {}, byModel = {},
	}

	for _, m in ipairs(dass.chat.get_messages()) do
		s.total = s.total + 1
		if m.role == "user" then
			s.user = s.user + 1
		elseif m.role == "assistant" then
			s.assistant = s.assistant + 1
		else
			s.other = s.other + 1
		end

		local u = m.usage
		if u ~= nil then
			local input = u.input_tokens or 0
			local cached = u.input_cache_hit_tokens or 0
			local missed = u.input_cache_miss_tokens or 0
			local written = u.cache_create_tokens or 0
			local output = u.output_tokens or 0
			local name = u.model_name or "unknown"

			s.turns = s.turns + 1
			s.input = s.input + input
			s.cached = s.cached + cached
			s.missed = s.missed + missed
			s.written = s.written + written
			s.output = s.output + output
			s.ttft = s.ttft + (u.time_to_first_token or 0)
			s.gen = s.gen + (u.generation_time or 0)

			local rec = s.byModel[name]
			if rec == nil then
				rec = { name = name, turns = 0, input = 0, cached = 0, missed = 0, written = 0, output = 0 }
				s.byModel[name] = rec
				s.models[#s.models + 1] = rec
			end
			rec.turns = rec.turns + 1
			rec.input = rec.input + input
			rec.cached = rec.cached + cached
			rec.missed = rec.missed + missed
			rec.written = rec.written + written
			rec.output = rec.output + output
		end
	end

	table.sort(s.models, function(a, b)
		return (a.input + a.output) > (b.input + b.output)
	end)
	return s
end

-- Everything the card and the model-facing text are made of, computed once per run.
local function metrics(s)
	local uncached = uncached_of(s.cached, s.missed, s.written, s.input)
	local requests = s.cached + uncached
	local m = {
		hasUsage = s.turns > 0,
		rate = requests > 0 and s.cached / requests or 0,
		uncached = uncached,
		requests = requests,
		speed = s.gen > 0 and s.output / s.gen or 0,
		ttft = s.turns > 0 and s.ttft / s.turns or 0,
		rows = {},
	}

	m.scope = truncate(dass.chat.get_title() or "", 44)
	m.messages = string.format("%d message%s · %d from you · %d from agents%s",
		s.total, s.total == 1 and "" or "s", s.user, s.assistant,
		s.other > 0 and (" · " .. s.other .. " other") or "")

	m.inputTokens = compact(s.input)
	m.inputCaption = group(s.input) .. " tokens"
	if s.written > 0 then
		m.inputCaption = m.inputCaption .. " · " .. compact(s.written) .. " written to cache"
	end

	m.outputTokens = compact(s.output)
	m.outputCaption = group(s.output) .. " tokens"
	if s.gen > 0 then
		m.outputCaption = m.outputCaption .. " · " .. string.format("%.1f tok/s", m.speed)
	end

	if m.requests > 0 then
		m.cacheTokens = pct(m.rate)
		m.cacheCaption = compact(s.cached) .. " of " .. compact(m.requests) .. " reused"
	else
		m.cacheTokens = "-"
		m.cacheCaption = "no cache split reported"
	end

	if showModels then
		for _, rec in ipairs(s.models) do
			local parts = {}
			for segment in rec.name:gmatch("[^$]+") do
				parts[#parts + 1] = segment
			end

			local detail = {}
			if parts[1] ~= nil then
				detail[#detail + 1] = parts[1]
			end
			for i = 3, #parts do
				detail[#detail + 1] = parts[i]
			end
			detail[#detail + 1] = string.format("%d turn%s", rec.turns, rec.turns == 1 and "" or "s")

			local recUncached = uncached_of(rec.cached, rec.missed, rec.written, rec.input)
			local recRequests = rec.cached + recUncached

			local row = {}
			row.name = parts[2] or parts[1] or rec.name
			row.detail = table.concat(detail, " · ")
			row.hit = recRequests > 0 and pct(rec.cached / recRequests) or "-"
			row.tokens = "in " .. compact(rec.input) .. " · out " .. compact(rec.output)
			m.rows[#m.rows + 1] = row
		end
	end
	m.modelCount = string.format("%d model%s", #m.rows, #m.rows == 1 and "" or "s")

	local footer = {
		string.format("%d agent turn%s", s.assistant, s.assistant == 1 and "" or "s"),
	}
	if s.turns > 0 then
		footer[#footer + 1] = string.format("avg %.2f s to first token", m.ttft)
		footer[#footer + 1] = string.format("%s generating", duration(s.gen))
		footer[#footer + 1] = string.format("avg %s in / %s out per turn",
			compact(s.input / s.turns), compact(s.output / s.turns))
	end
	m.footer = table.concat(footer, " · ")

	m.emptyText = "No usage recorded in this chat yet. A model that reports no usage leaves nothing to count."
	return m
end

-- First render assigns the members directly, later renders go through notify; a plain
-- write after create_control stays frozen on screen.
local vm = {}

local function push(name, value)
	vm[name] = value
	if vm.notify ~= nil then
		vm:notify(name)
	end
end

local function apply(m)
	push("scope", m.scope)
	push("messages", m.messages)
	push("hasUsage", m.hasUsage)
	push("inputTokens", m.inputTokens)
	push("inputCaption", m.inputCaption)
	push("outputTokens", m.outputTokens)
	push("outputCaption", m.outputCaption)
	push("cacheTokens", m.cacheTokens)
	push("cacheCaption", m.cacheCaption)
	push("modelsVisible", showModels and m.hasUsage and #m.rows > 0)
	push("modelCount", m.modelCount)
	push("models", m.rows)
	push("emptyVisible", not m.hasUsage)
	push("emptyText", m.emptyText)
	push("footer", m.footer)
end

-- The card carries the shape, this text carries the numbers: a reload loses the card's
-- closures, while the text stays in the history where the model can still read it.
local function summary(s, m)
	local lines = {}
	lines[#lines + 1] = "**Chat usage** - " .. (m.scope ~= "" and m.scope or "untitled chat")
	lines[#lines + 1] = m.messages

	if s.turns == 0 then
		lines[#lines + 1] = m.emptyText
		return table.concat(lines, "\n")
	end

	lines[#lines + 1] = string.format("- input: %s tokens, %s served from the prompt cache (%s reused, %s uncached)",
		group(s.input), pct(m.rate), group(s.cached), group(m.uncached))
	lines[#lines + 1] = string.format("- output: %s tokens over %s generating - %.1f tokens per second",
		group(s.output), duration(s.gen), m.speed)
	lines[#lines + 1] = string.format("- %d agent turns, %.2f s to the first token on average",
		s.turns, m.ttft)

	if showModels and #m.rows > 0 then
		lines[#lines + 1] = "- by model:"
		for _, row in ipairs(m.rows) do
			lines[#lines + 1] = string.format("  - %s (%s): %s, cache %s",
				row.name, row.detail, row.tokens, row.hit)
		end
	end
	return table.concat(lines, "\n")
end

vm.refresh = function(self)
	apply(metrics(gather()))
end

local xaml = [[
<Border xmlns="https://github.com/avaloniaui"
        Background="#0D1117" BorderBrush="#30363D" BorderThickness="1"
        CornerRadius="10" Padding="16" MinWidth="480">
  <StackPanel Spacing="12">

    <Grid ColumnDefinitions="*,Auto">
      <StackPanel Spacing="4" VerticalAlignment="Center">
        <StackPanel Orientation="Horizontal" Spacing="8">
          <TextBlock Text="Chat usage" FontSize="16" FontWeight="Bold" Foreground="#E6EDF3"/>
          <TextBlock Text="{Binding scope}" FontSize="12" Foreground="#6E7681" VerticalAlignment="Bottom"/>
        </StackPanel>
        <TextBlock Text="{Binding messages}" FontSize="11" Foreground="#8B949E"/>
      </StackPanel>
      <Button Grid.Column="1" Content="⟳  Refresh" Command="{Binding refresh}"
              VerticalAlignment="Top" Cursor="Hand"/>
    </Grid>

    <Grid ColumnDefinitions="*,8,*,8,*" IsVisible="{Binding hasUsage}">
      <Border Grid.Column="0" Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
              CornerRadius="8" Padding="12,10">
        <StackPanel Spacing="3">
          <TextBlock Text="INPUT" FontSize="9" Foreground="#6E7681"/>
          <TextBlock Text="{Binding inputTokens}" FontSize="20" FontWeight="Bold"
                     FontFamily="Consolas" Foreground="#E6EDF3"/>
          <TextBlock Text="{Binding inputCaption}" FontSize="10" Foreground="#8B949E" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
      <Border Grid.Column="2" Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
              CornerRadius="8" Padding="12,10">
        <StackPanel Spacing="3">
          <TextBlock Text="OUTPUT" FontSize="9" Foreground="#6E7681"/>
          <TextBlock Text="{Binding outputTokens}" FontSize="20" FontWeight="Bold"
                     FontFamily="Consolas" Foreground="#E6EDF3"/>
          <TextBlock Text="{Binding outputCaption}" FontSize="10" Foreground="#8B949E" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
      <Border Grid.Column="4" Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
              CornerRadius="8" Padding="12,10">
        <StackPanel Spacing="3">
          <TextBlock Text="CACHE HIT" FontSize="9" Foreground="#6E7681"/>
          <TextBlock Text="{Binding cacheTokens}" FontSize="20" FontWeight="Bold"
                     FontFamily="Consolas" Foreground="#58A6FF"/>
          <TextBlock Text="{Binding cacheCaption}" FontSize="10" Foreground="#8B949E" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
    </Grid>

    <StackPanel IsVisible="{Binding modelsVisible}">
      <Grid ColumnDefinitions="*,Auto" Margin="0,0,0,6">
        <TextBlock Text="By model" FontSize="13" FontWeight="SemiBold" Foreground="#E6EDF3"/>
        <TextBlock Grid.Column="1" Text="{Binding modelCount}" FontSize="10" Foreground="#6E7681"
                   VerticalAlignment="Center"/>
      </Grid>
      <ItemsControl ItemsSource="{Binding models}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
                    CornerRadius="8" Padding="12,9" Margin="0,0,0,6">
              <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2" VerticalAlignment="Center">
                  <TextBlock Text="{Binding name}" FontFamily="Consolas" FontSize="12"
                             FontWeight="SemiBold" Foreground="#E6EDF3"/>
                  <TextBlock Text="{Binding detail}" FontSize="10" Foreground="#8B949E"
                             TextWrapping="Wrap"/>
                </StackPanel>
                <StackPanel Grid.Column="1" Spacing="2" HorizontalAlignment="Right"
                            VerticalAlignment="Center" Margin="12,0,0,0">
                  <TextBlock Text="{Binding hit}" FontFamily="Consolas" FontSize="13"
                             FontWeight="SemiBold" Foreground="#58A6FF" HorizontalAlignment="Right"/>
                  <TextBlock Text="{Binding tokens}" FontSize="10" Foreground="#6E7681"
                             HorizontalAlignment="Right"/>
                </StackPanel>
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>

    <Border Background="#161B22" BorderBrush="#21262D" BorderThickness="1" CornerRadius="8"
            Padding="12" IsVisible="{Binding emptyVisible}">
      <TextBlock Text="{Binding emptyText}" FontSize="11" Foreground="#8B949E" TextWrapping="Wrap"/>
    </Border>

    <TextBlock Text="{Binding footer}" FontSize="10" Foreground="#6E7681" TextWrapping="Wrap"/>
  </StackPanel>
</Border>
]]

local state = gather()
local m = metrics(state)
apply(m)

local msg = dass.command.context.get_message()
if msg == nil then
	error("usage-stats only runs as a slash command.")
end

msg:append_data(dass.ui.create_control(xaml, vm))

-- The part is what the model reads: the card itself is user-only and does not travel to it.
msg:append_part{ content = summary(state, m), title = "Chat usage", icon = "PieChart", color = "#58A6FF" }
