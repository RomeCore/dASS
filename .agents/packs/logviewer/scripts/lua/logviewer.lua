--[[
name: logviewer
title: Log viewer
description: Owns the chat's log viewer tab - reads the shared log buffer, keeps a live subscription to it, and renders the level/text filtered tail as an interactive page.
namespace: logviewer
manuals: |
  --- logviewer - the log viewer of this chat

  The reader on top of dass.log. The buffer is application-wide, so the page shows this chat's own
  output together with every other subsystem's. The tab is created when the script loads and belongs
  to the script, the same way the cost overlay does: a tool call only reads the numbers back and
  never creates or moves anything.

  FUNCTIONS:

  --- logviewer.stats([level], [text]) -> table
  The counts behind the viewer: total (the whole buffer), shown (matching the filter),
  viewer_entries and viewer_paused (the open page), by_level, oldest and newest sequences,
  dropped, level and text (the filter used). Without arguments the filter is the one the tab is
  on; with them the report is filtered on its own and the tab is left alone.

  --- logviewer.tail([limit], [level], [text]) -> table
    The newest matching entries as { seq, time, level, message } rows, oldest first. Default 15.

  --- logviewer.count([level], [text]) -> integer
    How many buffered entries match the filter.
]]

local levels = { "verbose", "debug", "info", "warning", "error", "fatal" }

-- Mid-tone on purpose: the page follows the theme, so a colour has to read on both backgrounds.
local levelColors = {
	verbose = "#8C8C8C",
	debug = "#7FA6C9",
	info = "#4FA3D1",
	warning = "#D8A21C",
	error = "#E06C75",
	fatal = "#FF5370",
}

local maxEntries = 200
local currentLevel = "debug"
local currentText = ""

local liveVm = nil
local subscription = nil
local flushJob = nil
local dirty = false
local baseCount = dass.log.count()

local function levelIndex(name)
	for position, level in ipairs(levels) do
		if level == name then return position end
	end
	return nil
end

local function clock(timestamp)
	return datetime.format(datetime.from_timestamp(timestamp), "HH:mm:ss.fff")
end

local function row(entry)
	local detail = entry.template
	if entry.properties then
		local parts = {}
		for key, value in pairs(entry.properties) do
			parts[#parts + 1] = key .. "=" .. tostring(value)
		end
		if #parts > 0 then
			table.sort(parts)
			detail = detail .. "\n" .. table.concat(parts, "  ")
		end
	end
	if entry.exception then
		detail = detail .. "\n" .. entry.exception
	end

	return {
		time = clock(entry.timestamp),
		level = entry.level,
		level_color = levelColors[entry.level] or levelColors.info,
		message = entry.message,
		detail = detail,
	}
end

local function passes(entry, level, text)
	local minimum = levelIndex(level) or 1
	local actual = levelIndex(entry.level) or 1
	if actual < minimum then return false end
	if text ~= "" then
		-- dass.log matches case-insensitively; the viewer has to agree with what query returns.
		local needle = text:lower()
		if not entry.message:lower():find(needle, 1, true)
			and not entry.template:lower():find(needle, 1, true) then
			return false
		end
	end
	return true
end

local function query(level, text, limit)
	return dass.log.query({
		min_level = level,
		text = text ~= "" and text or nil,
		last = limit or maxEntries,
	})
end

local function collect(level, text)
	local rows = {}
	for _, entry in ipairs(query(level, text)) do
		rows[#rows + 1] = row(entry)
	end
	return rows
end

-- A revived command has no upvalues but resolves globals, so it reaches the current namespace.
local function put(vm, name, value)
	if vm.set ~= nil then
		vm:set(name, value)
	else
		vm[name] = value
	end
end

local function status(rows)
	return string.format("%d shown · %d logged", #rows, dass.log.count())
end

local function fill(vm)
	local rows = collect(currentLevel, currentText)
	dirty = false
	put(vm, "entries", rows)
	put(vm, "empty", #rows == 0)
	put(vm, "status", status(rows))
end

-- Appending to the member the view model already holds raises nothing: a new array is what makes
-- the list re-render. That is a reset per push, so new entries are coalesced into one every tick.
local function flush()
	local vm = liveVm
	if vm == nil or not dirty then return end
	dirty = false

	local source = vm.entries
	local rows = {}
	for index = 1, #source do
		rows[index] = source[index]
	end

	put(vm, "entries", rows)
	put(vm, "empty", #rows == 0)
	put(vm, "status", status(rows))
end

local xaml = [==[
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:smooth="using:LLMDesktopAssistant.Controls.Scrolling"
      RowDefinitions="Auto,Auto,*" RowSpacing="12">
  <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
    <ComboBox Width="130" ItemsSource="{Binding levels}"
              SelectedItem="{Binding level, Mode=TwoWay}"/>
    <TextBox Width="240" Watermark="filter text" Text="{Binding text, Mode=TwoWay}"/>
    <Button Classes="ghost" Content="{Binding pause_label}" Command="{Binding toggle_pause}"/>
    <Button Classes="ghost" Content="Clear" Command="{Binding clear}"/>
    <ToggleButton Content="Follow" Padding="12,4"
                  IsChecked="{Binding follow_end, Mode=TwoWay}"/>
    <TextBlock VerticalAlignment="Center" Foreground="{DynamicResource TextSecondary}"
               Text="{Binding status}"/>
  </StackPanel>

  <Grid Grid.Row="1" ColumnDefinitions="112,72,*" Margin="0,4,0,4">
    <TextBlock Grid.Column="0" Text="Time" FontWeight="SemiBold"
               FontFamily="Cascadia Mono, Consolas, monospace"
               Foreground="{DynamicResource TextSecondary}"/>
    <TextBlock Grid.Column="1" Text="Level" FontWeight="SemiBold"
               FontFamily="Cascadia Mono, Consolas, monospace"
               Foreground="{DynamicResource TextSecondary}"/>
    <TextBlock Grid.Column="2" Text="Message" FontWeight="SemiBold"
               FontFamily="Cascadia Mono, Consolas, monospace"
               Foreground="{DynamicResource TextSecondary}"/>
  </Grid>

  <smooth:SmoothScrollViewer Grid.Row="2" FollowEnd="{Binding follow_end}"
                             HorizontalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding entries}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Grid ColumnDefinitions="112,72,*" Margin="0,1">
            <TextBlock Grid.Column="0" Text="{Binding time}"
                       FontFamily="Cascadia Mono, Consolas, monospace"
                       FontSize="{DynamicResource FontSizeS}"
                       Foreground="{DynamicResource TextTertiary}"/>
            <TextBlock Grid.Column="1" Text="{Binding level}"
                       FontFamily="Cascadia Mono, Consolas, monospace"
                       FontSize="{DynamicResource FontSizeS}"
                       Foreground="{Binding level_color}"/>
            <TextBlock Grid.Column="2" Text="{Binding message}" TextWrapping="Wrap"
                       FontFamily="Cascadia Mono, Consolas, monospace"
                       FontSize="{DynamicResource FontSizeS}"
                       ToolTip.Tip="{Binding detail}"/>
          </Grid>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </smooth:SmoothScrollViewer>

  <TextBlock Grid.Row="2" IsVisible="{Binding empty}" Text="No entries match the filter."
             HorizontalAlignment="Center" VerticalAlignment="Top" Margin="0,24,0,0"
             Foreground="{DynamicResource TextSecondary}"/>
</Grid>
]==]

local function create()
	local vm = {
		levels = levels,
		level = currentLevel,
		text = currentText,
		paused = false,
		follow_end = true,
		pause_label = "Pause",
		status = "",
		empty = false,
		entries = collect(currentLevel, currentText),
	}

	vm.__changed = function(self, name) logviewer.on_changed(self, name) end
	vm.toggle_pause = function(self) logviewer.toggle_pause(self) end
	vm.clear = function(self) logviewer.clear(self) end

	local control = dass.ui.create_control(xaml, vm)
	dass.chat.append_data(control, {
		surface = "tab",
		title = "Logs",
		icon = "Article",
		closeable = false,
		replacement_key = "logviewer",
	})

	liveVm = vm
	fill(vm)
end

local function deliver(entry)
	local vm = liveVm
	if vm == nil or vm.paused then return end
	if not passes(entry, currentLevel, currentText) then return end

	local rows = vm.entries
	rows[#rows + 1] = row(entry)
	while #rows > maxEntries do
		table.remove(rows, 1)
	end
	dirty = true
end

local function ensure_subscription()
	if flushJob == nil then
		flushJob = dass.schedule.every(flush, { period = 250 })
	end

	if subscription ~= nil and subscription.registered then return end

	subscription = dass.log.subscribe({}, deliver)
	if not subscription.registered then
		dass.log.warning("logviewer: the chat is closing, the viewer will not follow new entries")
	end
end

logviewer.on_changed = function(self, name)
	if name ~= "level" and name ~= "text" then return end
	currentLevel = self.level or currentLevel
	currentText = self.text or ""
	fill(self)
end

logviewer.toggle_pause = function(self)
	local paused = not self.paused
	put(self, "paused", paused)
	put(self, "pause_label", paused and "Resume" or "Pause")
end

logviewer.clear = function(self)
	dirty = false
	put(self, "entries", {})
	put(self, "empty", true)
	put(self, "status", "cleared")
end

logviewer.count = function(level, text)
	return #query(level or currentLevel, text or currentText)
end

logviewer.tail = function(limit, level, text)
	local rows = {}
	local samples = query(level or currentLevel, text or currentText)
	local from = math.max(1, #samples - (limit or 20) + 1)
	for index = from, #samples do
		local entry = samples[index]
		rows[#rows + 1] = {
			seq = entry.seq,
			time = clock(entry.timestamp),
			level = entry.level,
			message = entry.message,
		}
	end
	return rows
end

logviewer.stats = function(level, text)
	level = level or currentLevel
	text = text or currentText

	local samples = query(level, text)
	local byLevel = {}
	for _, name in ipairs(levels) do
		byLevel[name] = 0
	end

	local oldest, newest = nil, nil
	for _, entry in ipairs(samples) do
		byLevel[entry.level] = (byLevel[entry.level] or 0) + 1
		oldest = oldest or entry.seq
		newest = entry.seq
	end

	return {
		total = dass.log.count(),
		shown = #samples,
		viewer_entries = liveVm ~= nil and #liveVm.entries or 0,
		viewer_paused = liveVm ~= nil and liveVm.paused or false,
		by_level = byLevel,
		oldest = oldest,
		newest = newest,
		dropped = math.max(0, dass.log.count() - baseCount - #samples),
		level = level,
		text = text,
	}
end

-- The tab belongs to the chat, not to a tool call, so it is created when the script loads - the same
-- way the cost overlay is. A failed append must not kill the whole namespace.
local ok, err = pcall(function()
	create()
	ensure_subscription()
end)
if not ok then
	dass.log.warning("logviewer: the tab could not be created at load: {Error}", tostring(err))
end
