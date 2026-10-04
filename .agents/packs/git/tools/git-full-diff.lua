--[[
title: Full Git Diff
description: Shows the full git diff of a repository - changes in tracked files plus the complete content of untracked files with no line truncation - and renders a summary statistics panel in the UI where untracked file line counts are added to the git diff stats. Use it before committing to review every local change, including new files, in a single call.
category: Git
behaviours:
  - execute-external-process
  - file-read
argument_schema: >-
  {
    "type": "object",
    "properties": {
      "path": {
        "type": "string",
        "description": "Path to the git repository relative to the working directory. Defaults to '.' - the current directory."
      }
    },
    "required": []
  }
]]
local args = tool_args or {}
-- Fields missing from the call arrive as a null value (not nil), so check the type.
local repo = "."
if type(args.path) == "string" and args.path ~= "" then repo = args.path end

local res = dass.tool.result
res.use_markdown(true)
res.set_status("SourceCommit", "Collecting git diff...")

if not process.which("git") then
	res.write("**Error:** git executable not found in PATH.")
	res.complete_with_error()
	return
end

-- ============================== helpers ==============================
local function norm(p) return (tostring(p):gsub("\\", "/")) end

local function repo_join(rel)
	local r = norm(repo):gsub("/+$", "")
	if r == "" or r == "." then return norm(rel) end
	return r .. "/" .. norm(rel)
end

local function count_lines(s)
	if s == nil or s == "" then return 0 end
	local n = regex.split("\n", s)
	local count = #n
	if string.sub(s, -1) == "\n" then count = count - 1 end
	return count
end

local function split_lines(s)
	if s == nil or s == "" then return {} end
	return regex.split("\r?\n", s)
end

local function fence_for(s)
	local max = 2
	for run in string.gmatch(s or "", "`+") do
		if #run > max then max = #run end
	end
	return string.rep("`", max + 1)
end

local function trim(s) return (tostring(s or ""):gsub("^%s+", ""):gsub("%s+$", "")) end

local async function git(...)
	return await process.exec("git", { "--no-pager", ... }, { cwd = repo, timeout_seconds = 180 })
end

-- ============================ tracked diff ============================
local diff_res = await git("diff")
local numstat_res = await git("-c", "core.quotepath=false", "diff", "--numstat")

if diff_res.exit_code ~= 0 then
	res.set_status("AlertCircle", "git diff failed")
	res.write("**Error running `git diff`:** " .. trim(diff_res.stderr))
	res.complete_with_error()
	return
end

local tracked = {}
local tracked_add, tracked_del = 0, 0
if numstat_res.exit_code == 0 then
	for _, line in ipairs(split_lines(numstat_res.stdout)) do
		local m = regex.match("^([^\\t]+)\\t([^\\t]+)\\t(.*)$", line)
		local a, d, p = nil, nil, nil
		if m then
			a = m.groups[1].value
			d = m.groups[2].value
			p = m.groups[3].value
		end
		if p and p ~= "" then
			local binary = (a == "-" or d == "-")
			local add = tonumber(a) or 0
			local del = tonumber(d) or 0
			table.insert(tracked, { path = p, add = add, del = del, binary = binary })
			tracked_add = tracked_add + add
			tracked_del = tracked_del + del
		end
	end
end

-- =========================== untracked files ==========================
local ls = await git("-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
local untracked_paths = {}
if ls.exit_code == 0 then
	for _, line in ipairs(split_lines(ls.stdout)) do
		if line ~= "" then table.insert(untracked_paths, line) end
	end
end

local untracked_items = {}
local untracked_lines = 0
for _, rel in ipairs(untracked_paths) do
	local wp = repo_join(rel)
	local binary, size = false, 0
	local okd, info = pcall(fs.detail, wp)
	if okd and info then
		binary = info.is_binary == true
		size = info.size or 0
	end

	local content, lines = nil, 0
	if not binary then
		local okr, c = pcall(fs.read, wp)
		if okr and type(c) == "string" then
			content = c
			lines = count_lines(c)
		end
	end

	table.insert(untracked_items, { path = rel, lines = lines, binary = binary, size = size, content = content })
	untracked_lines = untracked_lines + lines
end

-- =============================== branch ===============================
local br = await git("rev-parse", "--abbrev-ref", "HEAD")
local branch = "?"
if br.exit_code == 0 and trim(br.stdout) ~= "" then branch = trim(br.stdout) end

-- =============================== totals ===============================
local total_add = tracked_add + untracked_lines
local total_del = tracked_del
local files_changed = #tracked + #untracked_items

local max_line = 0
for _, e in ipairs(tracked) do
	local t = e.add + e.del
	if t > max_line then max_line = t end
end
for _, e in ipairs(untracked_items) do
	if e.lines > max_line then max_line = e.lines end
end

-- ============================== view model ============================
local FILE_W = 110
local TOTAL_W = 420

local function file_bar(add, del)
	local aw, dw = 0, 0
	if max_line > 0 and (add + del) > 0 then
		aw = math.floor(FILE_W * add / max_line + 0.5)
		dw = math.floor(FILE_W * del / max_line + 0.5)
		if add > 0 and aw < 2 then aw = 2 end
		if del > 0 and dw < 2 then dw = 2 end
	end
	return aw, aw, dw
end

local function make_row(path, add, del, badge)
	local aw, dl, dw = file_bar(add, del)
	return {
		path = path,
		badge = badge,
		addWidth = aw,
		delLeft = dl,
		delWidth = dw,
		statAdd = add > 0 and ("+" .. add) or "",
		statDel = del > 0 and ("−" .. del) or "",
	}
end

local tracked_rows = {}
for _, e in ipairs(tracked) do
	table.insert(tracked_rows, make_row(e.path, e.add, e.del, e.binary and "B" or "M"))
end

local untracked_rows = {}
for _, e in ipairs(untracked_items) do
	table.insert(untracked_rows, make_row(e.path, e.lines, 0, e.binary and "B" or "A"))
end

local tot = total_add + total_del
local t_add_w, t_del_left, t_del_w = 0, 0, 0
if tot > 0 then
	if total_add > 0 then t_add_w = math.floor(TOTAL_W * total_add / tot + 0.5) end
	if total_del > 0 then
		t_del_left = t_add_w
		t_del_w = TOTAL_W - t_add_w
	end
end

local vm = {
	title = "Git Diff Summary",
	pathLabel = repo,
	branchLabel = "⎇ " .. branch,
	filesLabel = files_changed .. " files",
	insertions = "+" .. total_add,
	deletions = "−" .. total_del,
	addBarWidth = t_add_w,
	delBarLeft = t_del_left,
	delBarWidth = t_del_w,
	barVisible = tot > 0,
	cleanVisible = files_changed == 0,
	note = "Untracked file lines are counted as insertions.",
	trackedHeader = "Modified (tracked) — " .. #tracked,
	trackedVisible = #tracked > 0,
	untrackedHeader = "New (untracked) — " .. #untracked_items .. " files, " .. untracked_lines .. " lines",
	untrackedVisible = #untracked_items > 0,
	trackedFiles = tracked_rows,
	untrackedFiles = untracked_rows,
}

-- ================================ content =============================
local md = {}
local function add(s) table.insert(md, s) end

add("# Git Full Diff")
add("")
add("**Path:** `" .. repo .. "`   **Branch:** `" .. branch .. "`")
add("")
add(string.format("**Total:** %d files changed, **+%d** / **−%d**", files_changed, total_add, total_del))
if #untracked_items > 0 then
	add(string.format("_Untracked: %d file(s), %d lines — counted as insertions._", #untracked_items, untracked_lines))
end
add("")
add("| File | + | − |")
add("|---|---:|---:|")
for _, e in ipairs(tracked) do
	add("| `" .. e.path .. "` | " .. (e.binary and "bin" or e.add) .. " | " .. (e.binary and "bin" or e.del) .. " |")
end
for _, e in ipairs(untracked_items) do
	add("| `" .. e.path .. "` _(new)_ | " .. (e.binary and "bin" or e.lines) .. " | 0 |")
end
add(string.format("| **Total** | **%d** | **%d** |", total_add, total_del))
add("")

add("## Diff of tracked files")
add("")
if trim(diff_res.stdout) == "" then
	add("_No changes in tracked files._")
else
	local f = fence_for(diff_res.stdout)
	add(f .. "diff")
	add((diff_res.stdout:gsub("\n+$", "")))
	add(f)
end

if #untracked_items > 0 then
	add("")
	add("## Untracked files (" .. #untracked_items .. ")")
	for _, e in ipairs(untracked_items) do
		add("")
		add("### `" .. e.path .. "`")
		add("")
		if e.binary then
			add(string.format("_Binary file (%d bytes) — content is not shown._", e.size))
		else
			local body = e.content or ""
			local f = fence_for(body)
			add(f)
			add((body:gsub("\n+$", "")))
			add(f)
		end
	end
end

res.write(table.concat(md, "\n"))
res.set_structured({
	path = repo,
	branch = branch,
	filesChanged = files_changed,
	trackedFiles = #tracked,
	untrackedFiles = #untracked_items,
	insertions = total_add,
	deletions = total_del,
	untrackedLines = untracked_lines,
})

-- ================================== UI ================================
res.append_data(dass.ui.create_control([[
<Border xmlns="https://github.com/avaloniaui"
        Background="#0D1117" BorderBrush="#30363D" BorderThickness="1"
        CornerRadius="8" Padding="16">
  <StackPanel Spacing="12">

    <StackPanel Orientation="Horizontal" Spacing="10">
      <TextBlock Text="{Binding title}" FontSize="15" FontWeight="Bold" Foreground="#E6EDF3"/>
      <Border Background="#21262D" CornerRadius="4" Padding="6,1">
        <TextBlock Text="{Binding pathLabel}" FontFamily="Consolas" FontSize="12" Foreground="#8B949E"/>
      </Border>
    </StackPanel>

    <TextBlock Text="Working tree is clean — no changes"
               Foreground="#3FB950" IsVisible="{Binding cleanVisible}"/>

    <StackPanel Spacing="8" IsVisible="{Binding barVisible}">
      <StackPanel Orientation="Horizontal" Spacing="16">
        <TextBlock Text="{Binding branchLabel}" Foreground="#8B949E" FontSize="12"/>
        <TextBlock Text="{Binding filesLabel}" Foreground="#8B949E" FontSize="12"/>
        <TextBlock Text="{Binding insertions}" Foreground="#3FB950" FontWeight="Bold" FontSize="12"/>
        <TextBlock Text="{Binding deletions}" Foreground="#F85149" FontWeight="Bold" FontSize="12"/>
      </StackPanel>
      <Canvas Height="12" Width="420" HorizontalAlignment="Left" ClipToBounds="True">
        <Border Canvas.Left="0" Canvas.Top="0" Width="420" Height="12" Background="#21262D" CornerRadius="6"/>
        <Border Canvas.Left="0" Canvas.Top="0" Width="{Binding addBarWidth}" Height="12" Background="#2EA043"/>
        <Border Canvas.Left="{Binding delBarLeft}" Canvas.Top="0" Width="{Binding delBarWidth}" Height="12" Background="#F85149"/>
      </Canvas>
      <TextBlock Text="{Binding note}" Foreground="#6E7681" FontSize="11"/>
    </StackPanel>

    <TextBlock Text="{Binding trackedHeader}" Foreground="#8B949E" FontWeight="SemiBold" FontSize="12"
               IsVisible="{Binding trackedVisible}"/>
    <ItemsControl ItemsSource="{Binding trackedFiles}" IsVisible="{Binding trackedVisible}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Grid ColumnDefinitions="*,Auto" Margin="0,1">
            <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="8">
              <TextBlock Text="{Binding badge}" Width="12" Foreground="#8B949E" FontFamily="Consolas" FontSize="12"/>
              <TextBlock Text="{Binding path}" Foreground="#C9D1D9" FontFamily="Consolas" FontSize="12"/>
            </StackPanel>
            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="10">
              <Canvas Width="110" Height="10" VerticalAlignment="Center" ClipToBounds="True">
                <Border Canvas.Left="0" Canvas.Top="0" Width="110" Height="10" Background="#21262D" CornerRadius="5"/>
                <Border Canvas.Left="0" Canvas.Top="0" Width="{Binding addWidth}" Height="10" Background="#2EA043"/>
                <Border Canvas.Left="{Binding delLeft}" Canvas.Top="0" Width="{Binding delWidth}" Height="10" Background="#F85149"/>
              </Canvas>
              <TextBlock Text="{Binding statAdd}" Width="52" TextAlignment="Right" Foreground="#3FB950" FontFamily="Consolas" FontSize="12"/>
              <TextBlock Text="{Binding statDel}" Width="52" TextAlignment="Right" Foreground="#F85149" FontFamily="Consolas" FontSize="12"/>
            </StackPanel>
          </Grid>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>

    <TextBlock Text="{Binding untrackedHeader}" Foreground="#8B949E" FontWeight="SemiBold" FontSize="12"
               IsVisible="{Binding untrackedVisible}"/>
    <ItemsControl ItemsSource="{Binding untrackedFiles}" IsVisible="{Binding untrackedVisible}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Grid ColumnDefinitions="*,Auto" Margin="0,1">
            <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="8">
              <TextBlock Text="{Binding badge}" Width="12" Foreground="#3FB950" FontFamily="Consolas" FontSize="12"/>
              <TextBlock Text="{Binding path}" Foreground="#C9D1D9" FontFamily="Consolas" FontSize="12"/>
            </StackPanel>
            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="10">
              <Canvas Width="110" Height="10" VerticalAlignment="Center" ClipToBounds="True">
                <Border Canvas.Left="0" Canvas.Top="0" Width="110" Height="10" Background="#21262D" CornerRadius="5"/>
                <Border Canvas.Left="0" Canvas.Top="0" Width="{Binding addWidth}" Height="10" Background="#2EA043"/>
                <Border Canvas.Left="{Binding delLeft}" Canvas.Top="0" Width="{Binding delWidth}" Height="10" Background="#F85149"/>
              </Canvas>
              <TextBlock Text="{Binding statAdd}" Width="52" TextAlignment="Right" Foreground="#3FB950" FontFamily="Consolas" FontSize="12"/>
              <TextBlock Text="{Binding statDel}" Width="52" TextAlignment="Right" Foreground="#F85149" FontFamily="Consolas" FontSize="12"/>
            </StackPanel>
          </Grid>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>

  </StackPanel>
</Border>
]], vm))

res.set_status("SourceCommit", string.format("+%d −%d · %d files", total_add, total_del, files_changed))
res.complete_with_success()
