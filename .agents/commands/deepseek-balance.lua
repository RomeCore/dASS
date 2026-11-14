--[[
name: deepseek-balance
title: DeepSeek balance
description: Shows the DeepSeek API balance of the key in DEEPSEEK_API_KEY plus the current model prices - availability, per-currency totals, per-model token prices and how many output tokens the balance still buys. The same numbers are appended as text, so the model reads them too. Prices are parsed from api-docs.deepseek.com, cached for a day and fall back to a built-in table.
category: Chat
generate: true
model-facing: neutral
]]
-- DeepSeek balance card. The AXAML root MUST carry xmlns="https://github.com/avaloniaui",
-- or the dynamic control resolves no element types and renders an error box.
--
-- Prices live on the docs page and nowhere else - there is no pricing endpoint - so the
-- card parses the page, keeps the result in a per-user store for a day and falls back to
-- the table below when the page cannot be read. A number on screen is never invented.

local ENDPOINT = "https://api.deepseek.com/user/balance"
local PRICING_URL = "https://api-docs.deepseek.com/quick_start/pricing"
local PRICE_TTL = 7 * 24 * 60 * 60
local SYMBOLS = { USD = "$", CNY = "¥" }

-- Verified 2026-11-10. Only used when both the docs page and the cache are unavailable.
-- Columns follow FALLBACK_PRICES.models, values are peak then off-peak, USD per 1M tokens.
local FALLBACK_PRICES = {
	models = { "deepseek-flash", "deepseek-v4-pro" },
	rows = {
		{ key = "hit", peak = { 0.006, 0.044 }, off = { 0.003, 0.022 } },
		{ key = "miss", peak = { 0.3, 1.32 }, off = { 0.15, 0.66 } },
		{ key = "out", peak = { 1.2, 3.96 }, off = { 0.6, 1.98 } },
	},
}

local cache = store.user("deepseek-prices")

-- Thousands separators for the integer part, so a four-digit balance stays readable.
local function group(digits)
	local parts = {}
	for i = #digits, 1, -3 do
		table.insert(parts, 1, digits:sub(math.max(1, i - 2), i))
	end
	return table.concat(parts, ",")
end

-- The API sends a string already rounded to cents; show it as sent and only pad to two
-- decimals, so a future third digit would reach the card instead of being rounded away.
local function money(value)
	local text = tostring(value)
	local sign, int, frac = text:match("^(-?)(%d+)%.(%d+)$")
	if int == nil then
		return text
	end
	while #frac < 2 do
		frac = frac .. "0"
	end
	return sign .. group(int) .. "." .. frac
end

-- A token price can be 0.003, so the decimals must follow the magnitude.
local function price(value)
	local text = string.format("%.5f", value or 0)
	text = text:gsub("0+$", "")
	local dot = text:find("%.")
	if dot == nil then
		return text .. ".00"
	end
	while #text - dot < 2 do
		text = text .. "0"
	end
	return text
end

local function pair(peak, off)
	return price(peak) .. " / " .. price(off)
end

-- The card pads its separators for layout, so the same text straight into a sentence reads
-- as three spaces in the middle of it.
local function tighten(text)
	return (text:gsub("%s*·%s*", " · "))
end

local function compact(value)
	if value >= 1000000 then
		return string.format("%.1fM", value / 1000000)
	end
	if value >= 1000 then
		return string.format("%.0fK", value / 1000)
	end
	return string.format("%.0f", value)
end

local function symbol(code)
	return SYMBOLS[code] or code
end

local function span(seconds)
	if seconds == nil or seconds < 60 then
		return "just now"
	end
	if seconds < 3600 then
		return string.format("%d min", math.floor(seconds / 60))
	end
	return string.format("%.1f h", seconds / 3600)
end

-- Peak windows are 01:00-04:00 and 06:00-10:00 UTC on weekdays. DeepSeek also excludes
-- Chinese public holidays, which cannot be known from here - hence "by clock".
local function peak_now()
	local utc = datetime.now()
	local weekday = (math.floor(datetime.timestamp(utc) / 86400) + 4) % 7
	if weekday == 0 or weekday == 6 then
		return false
	end
	return (utc.hour >= 1 and utc.hour < 4) or (utc.hour >= 6 and utc.hour < 10)
end

-- The card is not the only consumer: summary() rebuilds the same numbers as text for the
-- model, so every value the card shows is mirrored here first.
local state = {
	usd = nil,
	prices = nil,
	by_key = nil,
	available = nil,
	rows = {},
	error = nil,
	footer = "",
	price_rows = {},
	price_source = "",
	estimate = "",
}

local async function fetch_balance()
	local key = os.env("DEEPSEEK_API_KEY")
	if key == nil or key == "" then
		return nil, "DEEPSEEK_API_KEY is not set in the environment."
	end
	local res = await web.request("GET", ENDPOINT, {
		headers = {
			["Authorization"] = "Bearer " .. key,
			["Accept"] = "application/json"
		},
		timeout = 15
	})
	if res.status_code ~= 200 then
		local body = tostring(res.content or ""):gsub("%s+", " ")
		if #body > 300 then
			body = body:sub(1, 300) .. "..."
		end
		return nil, string.format("HTTP %d %s - %s", res.status_code,
			tostring(res.status_description), body)
	end
	local ok, data = pcall(json.decode, res.content)
	if not ok or type(data) ~= "table" then
		return nil, "The API answered with something that is not JSON."
	end
	return data, nil, key
end

-- The docs render the price table as cells only, so the column count comes from the
-- first pricing row and the model names from the header row above it.
local async function fetch_prices()
	local res = await web.parse(PRICING_URL, "table td, table th")
	local cells = res.contents or {}
	if #cells == 0 then
		return nil, "the pricing page returned no cells"
	end

	local first = nil
	for index, cell in ipairs(cells) do
		if cell:match("^1M ") then
			first = index
			break
		end
	end
	if first == nil then
		return nil, "no per-token pricing rows on the page"
	end

	local columns = 0
	local probe = first + 2
	while probe <= #cells and cells[probe]:match("^%$%d") do
		columns = columns + 1
		probe = probe + 1
	end
	if columns == 0 then
		return nil, "the pricing rows carry no prices"
	end

	local models = {}
	for offset = 1, columns do
		local name = cells[1 + offset]
		if name == nil then
			return nil, "the model header row is shorter than the price columns"
		end
		models[offset] = name:gsub("%(%d+%)$", "")
	end

	local rows = {}
	local index = first
	while index <= #cells do
		local key = nil
		if cells[index]:match("^1M ") then
			local upper = string.upper(cells[index])
			if upper:find("CACHE HIT", 1, true) then
				key = "hit"
			elseif upper:find("CACHE MISS", 1, true) then
				key = "miss"
			elseif upper:find("OUTPUT", 1, true) then
				key = "out"
			end
		end
		if key == nil then
			index = index + 1
		else
			local row = { key = key, peak = {}, off = {} }
			index = index + 1
			while index <= #cells do
				local rate = string.lower(cells[index])
				if rate ~= "peak" and rate ~= "off-peak" then
					break
				end
				local target = row.peak
				if rate == "off-peak" then
					target = row.off
				end
				index = index + 1
				local slot = 1
				while slot <= columns and index <= #cells and cells[index]:match("^%$%d") do
					target[slot] = tonumber(cells[index]:sub(2))
					index = index + 1
					slot = slot + 1
				end
			end
			rows[#rows + 1] = row
		end
	end

	local seen = {}
	for _, row in ipairs(rows) do
		if #row.peak ~= columns or #row.off ~= columns then
			return nil, "the pricing table changed shape"
		end
		seen[row.key] = true
	end
	for _, key in ipairs({ "hit", "miss", "out" }) do
		if not seen[key] then
			return nil, "the pricing table lost the " .. key .. " row"
		end
	end
	return { models = models, rows = rows }
end

local function source_text(source, age)
	if source == "live" then
		return "prices: read from api-docs.deepseek.com just now"
	end
	local when = span(age)
	local ago = " ago"
	if when == "just now" then
		ago = ""
	end
	if source == "cache" then
		return "prices: cached " .. when .. ago
	end
	if source == "stale" then
		return "prices: cached " .. when .. ago .. " - the docs page could not be read"
	end
	return "prices: built-in fallback - the docs page could not be read"
end

local function build_price_rows(data)
	local by_key = {}
	for _, row in ipairs(data.rows or {}) do
		by_key[row.key] = row
	end

	local rows = {}
	for index, model in ipairs(data.models or {}) do
		local item = {}
		item.name = model
		local parts = {}
		if by_key.miss ~= nil then
			parts[#parts + 1] = "in " .. pair(by_key.miss.peak[index], by_key.miss.off[index])
		end
		if by_key.out ~= nil then
			parts[#parts + 1] = "out " .. pair(by_key.out.peak[index], by_key.out.off[index])
		end
		if by_key.hit ~= nil then
			parts[#parts + 1] = "cache hit " .. pair(by_key.hit.peak[index], by_key.hit.off[index])
		end
		item.detail = table.concat(parts, "   ·   ")
		rows[#rows + 1] = item
	end
	return rows, by_key
end

local function estimate_usd(usd, by_key, data, peak)
	local output = by_key.out
	if usd == nil or output == nil then
		return nil, nil
	end
	local rates = output.off
	if peak then
		rates = output.peak
	end
	local model, cheapest = nil, nil
	for index, name in ipairs(data.models or {}) do
		local value = rates[index]
		if value ~= nil and (cheapest == nil or value < cheapest) then
			model, cheapest = name, value
		end
	end
	if model == nil or cheapest == nil or cheapest <= 0 then
		return nil, nil
	end
	return model, usd / cheapest * 1000000
end

local vm = {}
vm.okVisible = false
vm.warnVisible = false
vm.hasPrimary = false
vm.primaryAmount = ""
vm.primaryCaption = ""
vm.rows = {}
vm.errorVisible = false
vm.errorText = ""
vm.priceRows = {}
vm.priceSource = "prices: loading..."
vm.hasEstimate = false
vm.estimate = ""
vm.peakVisible = false
vm.offPeakVisible = false
vm.footer = "Reading the key and calling the API..."

local function apply_estimate()
	local peak = peak_now()
	vm:set("peakVisible", peak)
	vm:set("offPeakVisible", not peak)

	local model, tokens = estimate_usd(state.usd, state.by_key or {}, state.prices or {}, peak)
	if state.usd ~= nil and state.usd <= 0 then
		state.estimate = "USD balance is empty - nothing left to spend"
		vm:set("hasEstimate", true)
		vm:set("estimate", state.estimate)
		return
	end
	if model == nil then
		state.estimate = ""
		vm:set("hasEstimate", false)
		vm:set("estimate", "")
		return
	end
	local rate = "off-peak"
	if peak then
		rate = "peak"
	end
	state.estimate = string.format("%s rate now - about %s output tokens on %s",
		rate, compact(tokens), model)
	vm:set("hasEstimate", true)
	vm:set("estimate", state.estimate)
end

local function render_prices(data, source, age)
	local rows, by_key = build_price_rows(data)
	local note = source_text(source, age)
	state.prices = data
	state.by_key = by_key
	state.price_rows = rows
	state.price_source = note
	vm:set("priceRows", rows)
	vm:set("priceSource", note)
end

local async function load_prices(force)
	if not force then
		local cached = cache.get("data")
		local stamp = cache.get("fetched_at")
		if type(cached) == "table" and type(stamp) == "number" and time.now() - stamp < PRICE_TTL then
			render_prices(cached, "cache", time.now() - stamp)
			return
		end
	end

	local fetched, err
	try
		fetched, err = await fetch_prices()
	catch e do
		fetched, err = nil, tostring(e)
	end
	if fetched ~= nil then
		cache.set("data", fetched)
		cache.set("fetched_at", time.now())
		render_prices(fetched, "live", 0)
		return
	end
	dass.log.warning("deepseek-balance: prices unavailable - " .. tostring(err))

	local cached = cache.get("data")
	local stamp = cache.get("fetched_at")
	if type(cached) == "table" and type(stamp) == "number" then
		render_prices(cached, "stale", time.now() - stamp)
		return
	end
	render_prices(FALLBACK_PRICES, "fallback", nil)
end

local async function refresh()
	local data, err, key
	try
		data, err, key = await fetch_balance()
	catch e do
		data, err, key = nil, tostring(e), nil
	end

	if err ~= nil then
		state.usd = nil
		state.available = nil
		state.rows = {}
		state.error = err
		state.footer = "DeepSeek API - no balance to show"
		vm:set("okVisible", false)
		vm:set("warnVisible", false)
		vm:set("hasPrimary", false)
		vm:set("rows", {})
		vm:set("errorVisible", true)
		vm:set("errorText", err)
		vm:set("footer", state.footer)
	else
		local rows = {}
		for _, info in ipairs(data.balance_infos or {}) do
			local row = {}
			row.currency = tostring(info.currency)
			row.total = symbol(row.currency) .. " " .. money(info.total_balance)
			row.detail = string.format("granted %s %s   ·   topped up %s %s",
				symbol(row.currency), money(info.granted_balance),
				symbol(row.currency), money(info.topped_up_balance))
			table.insert(rows, row)
			if row.currency == "USD" then
				state.usd = tonumber(info.total_balance)
			end
		end

		state.error = nil
		state.rows = rows
		state.available = data.is_available == true
		vm:set("okVisible", data.is_available == true)
		vm:set("warnVisible", data.is_available ~= true)
		vm:set("errorVisible", false)
		vm:set("errorText", "")
		vm:set("rows", rows)
		vm:set("hasPrimary", #rows == 1)
		if #rows == 1 then
			vm:set("primaryAmount", rows[1].total)
			vm:set("primaryCaption", rows[1].currency .. " - total balance")
		else
			vm:set("primaryAmount", "")
			vm:set("primaryCaption", "")
		end
		local stamp = datetime.format(datetime.now(), "HH:mm:ss")
		state.footer = string.format("key %s***   ·   updated %s UTC   ·   balances are 2-decimal only",
			key:sub(1, 5), stamp)
		vm:set("footer", state.footer)
	end

	await load_prices(false)
	apply_estimate()
end

-- Everything the card carries, as text. This string is the model-facing content of the
-- command, so a number that is on the card and missing here is a number the model cannot see.
local function summary()
	local lines = {}
	local header = "**DeepSeek API - not available**"
	if state.error ~= nil then
		header = "**DeepSeek API - balance unavailable**"
	elseif state.available then
		header = "**DeepSeek API - available**"
	end
	lines[#lines + 1] = header

	if state.error ~= nil then
		lines[#lines + 1] = state.error
	else
		for _, row in ipairs(state.rows) do
			lines[#lines + 1] = string.format("- %s: %s - %s", row.currency, row.total, tighten(row.detail))
		end
	end
	lines[#lines + 1] = tighten(state.footer)

	-- The source line already opens with 'prices:', which reads as a sentence fragment here.
	local source = tighten((state.price_source:gsub("^prices: ", "Source: ")))
	lines[#lines + 1] = ""
	lines[#lines + 1] = "Model prices - USD per 1M tokens, peak / off-peak."
	lines[#lines + 1] = source
	for _, row in ipairs(state.price_rows) do
		lines[#lines + 1] = "- " .. row.name .. ": " .. tighten(row.detail)
	end

	if state.estimate ~= "" then
		lines[#lines + 1] = "What the balance buys: " .. state.estimate
	end
	return table.concat(lines, "\n")
end

vm.refresh = async function(self)
	await refresh()
end

vm.reloadPrices = async function(self)
	await load_prices(true)
	apply_estimate()
end

local xaml = [[
<Border xmlns="https://github.com/avaloniaui"
        Background="#0D1117" BorderBrush="#30363D" BorderThickness="1"
        CornerRadius="10" Padding="16" MinWidth="460">
  <StackPanel Spacing="12">

    <Grid ColumnDefinitions="*,Auto">
      <StackPanel Spacing="6" VerticalAlignment="Center">
        <StackPanel Orientation="Horizontal" Spacing="8">
          <TextBlock Text="DeepSeek" FontSize="16" FontWeight="Bold" Foreground="#E6EDF3"/>
          <TextBlock Text="API balance" FontSize="12" Foreground="#6E7681" VerticalAlignment="Bottom"/>
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="6">
          <Border Background="#12261E" BorderBrush="#1F6F45" BorderThickness="1" CornerRadius="9"
                  Padding="9,2" IsVisible="{Binding okVisible}">
            <TextBlock Text="Available" FontSize="11" Foreground="#3FB950"/>
          </Border>
          <Border Background="#2D1517" BorderBrush="#5A2A2E" BorderThickness="1" CornerRadius="9"
                  Padding="9,2" IsVisible="{Binding warnVisible}">
            <TextBlock Text="Not available" FontSize="11" Foreground="#F85149"/>
          </Border>
        </StackPanel>
      </StackPanel>
      <Button Grid.Column="1" Content="⟳  Refresh" Command="{Binding refresh}"
              VerticalAlignment="Top" Cursor="Hand"/>
    </Grid>

    <StackPanel Spacing="1" IsVisible="{Binding hasPrimary}">
      <TextBlock Text="{Binding primaryAmount}" FontSize="30" FontWeight="Bold"
                 FontFamily="Consolas" Foreground="#E6EDF3"/>
      <TextBlock Text="{Binding primaryCaption}" FontSize="11" Foreground="#8B949E"/>
    </StackPanel>

    <ItemsControl ItemsSource="{Binding rows}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Border Background="#161B22" BorderBrush="#21262D" BorderThickness="1"
                  CornerRadius="8" Padding="12,10" Margin="0,0,0,8">
            <Grid ColumnDefinitions="Auto,*,Auto">
              <Border Grid.Column="0" Background="#1F6FEB26" CornerRadius="6" Padding="8,3"
                      VerticalAlignment="Center" Margin="0,0,12,0">
                <TextBlock Text="{Binding currency}" FontSize="11" FontWeight="SemiBold"
                           FontFamily="Consolas" Foreground="#58A6FF"/>
              </Border>
              <TextBlock Grid.Column="1" Text="{Binding detail}" FontSize="11"
                         Foreground="#8B949E" VerticalAlignment="Center"/>
              <TextBlock Grid.Column="2" Text="{Binding total}" FontSize="15" FontWeight="Bold"
                         FontFamily="Consolas" Foreground="#E6EDF3"
                         VerticalAlignment="Center" Margin="12,0,0,0"/>
            </Grid>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>

    <Border Background="#2D1517" BorderBrush="#5A2A2E" BorderThickness="1" CornerRadius="8"
            Padding="10" IsVisible="{Binding errorVisible}">
      <TextBlock Text="{Binding errorText}" Foreground="#F85149" FontSize="12" TextWrapping="Wrap"/>
    </Border>

    <Border Background="#161B22" BorderBrush="#21262D" BorderThickness="1" CornerRadius="8" Padding="12">
      <StackPanel Spacing="8">
        <Grid ColumnDefinitions="*,Auto">
          <StackPanel Spacing="6" VerticalAlignment="Center">
            <TextBlock Text="Model prices" FontSize="13" FontWeight="SemiBold" Foreground="#E6EDF3"/>
            <TextBlock Text="USD per 1M tokens - peak / off-peak" FontSize="10" Foreground="#6E7681"/>
            <StackPanel Orientation="Horizontal" Spacing="6">
              <Border Background="#2A2118" BorderBrush="#7A5B18" BorderThickness="1" CornerRadius="9"
                      Padding="8,1" IsVisible="{Binding peakVisible}">
                <TextBlock Text="peak now - 2x" FontSize="10" Foreground="#D29922"/>
              </Border>
              <Border Background="#12261E" BorderBrush="#1F6F45" BorderThickness="1" CornerRadius="9"
                      Padding="8,1" IsVisible="{Binding offPeakVisible}">
                <TextBlock Text="off-peak now" FontSize="10" Foreground="#3FB950"/>
              </Border>
            </StackPanel>
          </StackPanel>
          <Button Grid.Column="1" Content="⟳" Command="{Binding reloadPrices}"
                  VerticalAlignment="Center" Cursor="Hand" Padding="8,2"/>
        </Grid>

        <ItemsControl ItemsSource="{Binding priceRows}">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <StackPanel Spacing="1" Margin="0,0,0,6">
                <TextBlock Text="{Binding name}" FontFamily="Consolas" FontSize="12"
                           FontWeight="SemiBold" Foreground="#E6EDF3"/>
                <TextBlock Text="{Binding detail}" FontSize="10" Foreground="#8B949E"
                           TextWrapping="Wrap"/>
              </StackPanel>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>

        <TextBlock Text="{Binding estimate}" FontSize="11" Foreground="#58A6FF"
                   TextWrapping="Wrap" IsVisible="{Binding hasEstimate}"/>
        <TextBlock Text="{Binding priceSource}" FontSize="10" Foreground="#6E7681"
                   TextWrapping="Wrap"/>
      </StackPanel>
    </Border>

    <TextBlock Text="{Binding footer}" FontSize="10" Foreground="#6E7681"/>
  </StackPanel>
</Border>
]]

local msg = dass.command.context.get_message()
if msg == nil then
	return false
end

msg:append_data(dass.ui.create_control(xaml, vm))
await refresh()

-- append_data is user-only, so the model would otherwise see a bare token. This part joins
-- the model-facing content; it is appended once, because the reload button refreshes the
-- card only and a second part would duplicate the balance in the history.
msg:append_part{ content = summary(), title = "DeepSeek balance", icon = "AccountBalanceWallet", color = "#58A6FF" }

return true
