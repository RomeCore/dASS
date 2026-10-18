--[[
name: gitlib
title: Git helpers
description: Shared git collection and rendering for the git-full-diff tool and the commit-message command. Publishes the `git` namespace.
namespace: git
manuals: |
  --- git — shared git helpers (addon pack 'git')

  Collects the working-tree state of one repository and renders it in the shapes
  the tool and the slash command need. Loaded once per chat's Lua state; `git` is
  nil when this script is disabled in chat settings, so every caller guards for it.

  FUNCTIONS:

  --- async git.collect(repo) -> table, string
    Runs every git query for `repo` (default ".") and returns the data table, or
    nil plus an error message.

    data = {
      repo, repo_name, branch, diff_label,
      status,       -- `git status --porcelain=v1 -b` text
      diff,         -- `git diff HEAD` text: staged AND unstaged changes together
      tracked,      -- array of { path, add, del, binary } (from `git diff HEAD --numstat`)
      untracked,    -- array of { path, lines, binary, size, content }
      recent,       -- 25 "oneline" commit subjects
      scopes,       -- array of { name, count }, conventional scopes of the last 500 commits
      totals,       -- { add, del, files, tracked_files, untracked_files, untracked_lines }
    }

    Untracked files are always read: the line counts feed the summary panel even
    when the caller does not render their content.

  --- git.summary_markdown(data) -> string
    Path, branch, totals and the per-file table.

  --- git.history_markdown(data) -> string
    The last 25 commits in a fence plus the `| scope | count |` table.

  --- git.diff_markdown(data) -> string
    The tracked diff (staged and unstaged) in a fence.

  --- git.untracked_markdown(data) -> string
    Every untracked file with its full content in a fence; binaries as a note.

  --- git.status_markdown(data) -> string
    `git status --porcelain=v1 -b` in a fence.

  --- git.structured(data) -> table
    The compact structured result the tool reports (counts plus recent commits).

  --- git.uicontrol(data) -> uicontrol
    The diff summary panel. Render it with `dass.tool.result.append_data` in a
    tool, or `msg:append_data` on a message in a command.

  NOTES:
  - `git diff HEAD` deliberately includes staged changes: a commit names the whole
    change, not only the part still unstaged.
  - A repository without commits has no HEAD, so the base falls back to
    `git diff --cached` and the branch reads "(no commits)".
]]

-- ============================== helpers ==============================

local function norm(p) return (tostring(p):gsub("\\", "/")) end

local function trim(s) return (tostring(s or ""):gsub("^%s+", ""):gsub("%s+$", "")) end

local function split_lines(s)
	if s == nil or s == "" then return {} end
	return regex.split("\r?\n", s)
end

local function count_lines(s)
	if s == nil or s == "" then return 0 end
	local n = regex.split("\n", s)
	local count = #n
	if string.sub(s, -1) == "\n" then count = count - 1 end
	return count
end

local function fence_for(s)
	local max = 2
	for run in string.gmatch(s or "", "`+") do
		if #run > max then max = #run end
	end
	return string.rep("`", max + 1)
end

local function repo_join(repo, rel)
	local r = norm(repo):gsub("/+$", "")
	if r == "" or r == "." then return norm(rel) end
	return r .. "/" .. norm(rel)
end

local async function run_git(cwd, ...)
	return await process.exec("git", { "--no-pager", ... }, { cwd = cwd, timeout_seconds = 180 })
end

-- Parses conventional-commit subjects into `{ name = <scope>, count = <n> }`, count desc,
-- capped at 25. Merge commits are skipped (their leading word would read as a type), and
-- anything that is not `type(scope)!: subject` / `type!: subject` is ignored.
local function collect_scopes(subjects)
	local counts, order = {}, {}

	for _, line in ipairs(subjects) do
		local s = trim(line)
		if s ~= "" and not string.find(s, "^Merge ") then
			local ty, rest = string.match(s, "^(%a[%w_]*)(.*)$")
			local scope = nil

			if ty and rest then
				if string.sub(rest, 1, 1) == "(" then
					local close = string.find(rest, ")", 2, true)
					if close then
						scope = string.sub(rest, 2, close - 1)
						rest = string.sub(rest, close + 1)
						if scope == "" then scope = nil end
					else
						ty = nil
					end
				end

				if ty then
					if string.sub(rest, 1, 1) == "!" then rest = string.sub(rest, 2) end
					if string.sub(rest, 1, 1) ~= ":" then ty = nil end
				end
			end

			if ty then
				local key = scope or "(no scope)"
				if counts[key] == nil then
					counts[key] = 0
					order[#order + 1] = key
				end
				counts[key] = counts[key] + 1
			end
		end
	end

	local list = {}
	for _, key in ipairs(order) do
		list[#list + 1] = { name = key, count = counts[key] }
	end

	table.sort(list, function(a, b)
		if a.count ~= b.count then return a.count > b.count end
		return a.name < b.name
	end)

	local capped = {}
	for i = 1, math.min(#list, 25) do
		capped[#capped + 1] = list[i]
	end
	return capped
end

-- ============================== collect ==============================

_NS.collect = async function(repo)
	repo = repo or "."

	if not process.which("git") then
		return nil, "git executable not found in PATH."
	end

	-- A fresh repository has no HEAD; then `--cached` is the only base that still shows work.
	local head = await run_git(repo, "rev-parse", "--verify", "HEAD")
	local has_head = head.exit_code == 0

	local diff_res, numstat_res, diff_label
	if has_head then
		diff_res = await run_git(repo, "diff", "HEAD")
		numstat_res = await run_git(repo, "-c", "core.quotepath=false", "diff", "HEAD", "--numstat")
		diff_label = "git diff HEAD"
	else
		diff_res = await run_git(repo, "diff", "--cached")
		numstat_res = await run_git(repo, "-c", "core.quotepath=false", "diff", "--cached", "--numstat")
		diff_label = "git diff --cached"
	end

	if diff_res.exit_code ~= 0 then
		return nil, "`" .. diff_label .. "` failed: " .. trim(diff_res.stderr)
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
				tracked[#tracked + 1] = { path = p, add = add, del = del, binary = binary }
				tracked_add = tracked_add + add
				tracked_del = tracked_del + del
			end
		end
	end

	local status_res = await run_git(repo, "-c", "core.quotepath=false", "status", "--porcelain=v1", "-b")
	local status = status_res.exit_code == 0 and status_res.stdout or ""

	local br = await run_git(repo, "rev-parse", "--abbrev-ref", "HEAD")
	local branch = "?"
	if has_head and br.exit_code == 0 and trim(br.stdout) ~= "" then
		branch = trim(br.stdout)
	elseif not has_head then
		branch = "(no commits)"
	end

	local repo_name = repo
	local top = await run_git(repo, "rev-parse", "--show-toplevel")
	if top.exit_code == 0 then
		local root = norm(trim(top.stdout)):gsub("/+$", "")
		if root ~= "" then
			local base = regex.match("([^/]+)$", root)
			if base then repo_name = base.groups[1].value else repo_name = root end
		end
	end

	local ls = await run_git(repo, "-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard")
	local untracked_paths = {}
	if ls.exit_code == 0 then
		for _, line in ipairs(split_lines(ls.stdout)) do
			if line ~= "" then untracked_paths[#untracked_paths + 1] = line end
		end
	end

	local untracked = {}
	local untracked_lines = 0

	for _, rel in ipairs(untracked_paths) do
		local wp = repo_join(repo, rel)
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

		untracked[#untracked + 1] = { path = rel, lines = lines, binary = binary, size = size, content = content }
		untracked_lines = untracked_lines + lines
	end

	local recent = {}
	if has_head then
		local log_res = await run_git(repo, "log", "--oneline", "-25")
		if log_res.exit_code == 0 then
			for _, line in ipairs(split_lines(log_res.stdout)) do
				local t = trim(line)
				if t ~= "" then recent[#recent + 1] = t end
			end
		end
	end

	local subjects = {}
	if has_head then
		local subj_res = await run_git(repo, "log", "--format=%s", "-500")
		if subj_res.exit_code == 0 then
			for _, line in ipairs(split_lines(subj_res.stdout)) do
				if line ~= "" then subjects[#subjects + 1] = line end
			end
		end
	end

	return {
		repo = repo,
		repo_name = repo_name,
		branch = branch,
		diff_label = diff_label,
		status = status,
		diff = diff_res.stdout,
		tracked = tracked,
		untracked = untracked,
		recent = recent,
		scopes = collect_scopes(subjects),
		totals = {
			add = tracked_add + untracked_lines,
			del = tracked_del,
			files = #tracked + #untracked,
			tracked_files = #tracked,
			untracked_files = #untracked,
			untracked_lines = untracked_lines,
		},
	}
end

-- ============================== markdown ==============================

_NS.summary_markdown = function(d)
	local md = {}
	local function add(s) md[#md + 1] = s end

	add(string.format("**Path:** `%s`   **Branch:** `%s`", d.repo, d.branch))
	add("")
	add(string.format("**Total:** %d files changed, **+%d** / **−%d**", d.totals.files, d.totals.add, d.totals.del))
	if d.totals.untracked_files > 0 then
		add(string.format("_Untracked: %d file(s), %d lines — counted as insertions._",
			d.totals.untracked_files, d.totals.untracked_lines))
	end
	if d.totals.files == 0 then
		add("")
		add("_Working tree is clean — no changes._")
	end
	add("")
	add("| File | + | − |")
	add("|---|---:|---:|")
	for _, e in ipairs(d.tracked) do
		add("| `" .. e.path .. "` | " .. (e.binary and "bin" or tostring(e.add)) .. " | " .. (e.binary and "bin" or tostring(e.del)) .. " |")
	end
	for _, e in ipairs(d.untracked) do
		add("| `" .. e.path .. "` _(new)_ | " .. (e.binary and "bin" or tostring(e.lines)) .. " | 0 |")
	end
	add(string.format("| **Total** | **%d** | **%d** |", d.totals.add, d.totals.del))

	return table.concat(md, "\n")
end

_NS.history_markdown = function(d)
	local md = {}
	local function add(s) md[#md + 1] = s end

	add(string.format("## Recent commits (`git log --oneline -%d`)", #d.recent))
	add("")
	if #d.recent == 0 then
		add("_No commits._")
	else
		local body = table.concat(d.recent, "\n")
		local f = fence_for(body)
		add(f)
		add(body)
		add(f)
	end

	add("")
	add("## Scopes across the last 500 commits")
	add("")
	if #d.scopes == 0 then
		add("_No conventional scopes in the history._")
	else
		add("| scope | count |")
		add("|---|---:|")
		for _, s in ipairs(d.scopes) do
			if s.name == "(no scope)" then
				add("| (no scope) | " .. s.count .. " |")
			else
				add("| `" .. s.name .. "` | " .. s.count .. " |")
			end
		end
	end

	return table.concat(md, "\n")
end

_NS.diff_markdown = function(d)
	local md = {}
	local function add(s) md[#md + 1] = s end

	add("## Diff (`" .. d.diff_label .. "`)")
	add("")
	if trim(d.diff) == "" then
		add("_No changes in tracked files._")
	else
		local f = fence_for(d.diff)
		add(f .. "diff")
		add((d.diff:gsub("\n+$", "")))
		add(f)
	end

	return table.concat(md, "\n")
end

_NS.untracked_markdown = function(d)
	local md = {}
	local function add(s) md[#md + 1] = s end

	add("## Untracked files (" .. #d.untracked .. ")")
	for _, e in ipairs(d.untracked) do
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

	return table.concat(md, "\n")
end

_NS.status_markdown = function(d)
	local md = {}
	local function add(s) md[#md + 1] = s end

	add("## Status (`git status --porcelain=v1 -b`)")
	add("")
	if trim(d.status) == "" then
		add("_Working tree is clean._")
	else
		local f = fence_for(d.status)
		add(f .. "text")
		add((d.status:gsub("\n+$", "")))
		add(f)
	end

	return table.concat(md, "\n")
end

-- ============================== structured ==============================

_NS.structured = function(d)
	return {
		path = d.repo,
		branch = d.branch,
		filesChanged = d.totals.files,
		trackedFiles = d.totals.tracked_files,
		untrackedFiles = d.totals.untracked_files,
		insertions = d.totals.add,
		deletions = d.totals.del,
		untrackedLines = d.totals.untracked_lines,
		recentCommits = d.recent,
		scopes = d.scopes,
	}
end

-- ================================ panel ================================

_NS.uicontrol = function(d)
	local FILE_W = 110
	local TOTAL_W = 420

	local max_line = 0
	for _, e in ipairs(d.tracked) do
		local t = e.add + e.del
		if t > max_line then max_line = t end
	end
	for _, e in ipairs(d.untracked) do
		if e.lines > max_line then max_line = e.lines end
	end

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
	for _, e in ipairs(d.tracked) do
		tracked_rows[#tracked_rows + 1] = make_row(e.path, e.add, e.del, e.binary and "B" or "M")
	end

	local untracked_rows = {}
	for _, e in ipairs(d.untracked) do
		untracked_rows[#untracked_rows + 1] = make_row(e.path, e.lines, 0, e.binary and "B" or "A")
	end

	local tot = d.totals.add + d.totals.del
	local t_add_w, t_del_left, t_del_w = 0, 0, 0
	if tot > 0 then
		if d.totals.add > 0 then t_add_w = math.floor(TOTAL_W * d.totals.add / tot + 0.5) end
		if d.totals.del > 0 then
			t_del_left = t_add_w
			t_del_w = TOTAL_W - t_add_w
		end
	end

	local vm = {
		title = "Git Diff Summary",
		pathLabel = d.repo_name,
		branchLabel = "⎇ " .. d.branch,
		filesLabel = d.totals.files .. " files",
		insertions = "+" .. d.totals.add,
		deletions = "−" .. d.totals.del,
		addBarWidth = t_add_w,
		delBarLeft = t_del_left,
		delBarWidth = t_del_w,
		barVisible = tot > 0,
		cleanVisible = d.totals.files == 0,
		note = "Untracked file lines are counted as insertions.",
		trackedHeader = "Modified (tracked) — " .. #d.tracked,
		trackedVisible = #d.tracked > 0,
		untrackedHeader = "New (untracked) — " .. #d.untracked .. " files, " .. d.totals.untracked_lines .. " lines",
		untrackedVisible = #d.untracked > 0,
		trackedFiles = tracked_rows,
		untrackedFiles = untracked_rows,
	}

	return dass.ui.create_control([[
<Border xmlns="https://github.com/avaloniaui"
        Background="#0D1117" BorderBrush="#30363D" BorderThickness="1"
        CornerRadius="8" Padding="16">
  <StackPanel Spacing="12">

    <StackPanel Orientation="Horizontal" Spacing="10">
      <TextBlock Text="{Binding title}" FontSize="15" FontWeight="Bold" Foreground="#E6EDF3"/>
      <Border Background="#21262D" CornerRadius="4" Padding="6,2" VerticalAlignment="Center">
        <TextBlock Text="{Binding pathLabel}" FontFamily="Consolas" FontSize="12" Foreground="#8B949E" VerticalAlignment="Center"/>
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
]], vm)
end
