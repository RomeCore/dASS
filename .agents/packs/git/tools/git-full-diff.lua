--[[
title: Full Git Diff
description: Shows the full git diff of a repository - changes in tracked files (staged and unstaged together, `git diff HEAD`) plus the complete content of untracked files with no line truncation - together with the last 25 commits and a scope histogram over the last 500 commits, and renders a summary statistics panel in the UI where untracked file line counts are added to the git diff stats. Use it before committing to review every local change, including new files, and to see which scopes the repository actually uses - it replaces a separate `git log`, `git status` or `git diff` call.
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

-- The collection and the rendering live in the 'gitlib' script (scripts/lua), shared with
-- the /commit-message command. Without it there is nothing to show, so fail loudly.
if git == nil then
	res.write("**Error:** the 'gitlib' Lua script is disabled. Enable it in Settings → Scripts, then run `/addon-reload`.")
	res.complete_with_error()
	return
end

local data, err = await git.collect(repo)
if not data then
	res.set_status("AlertCircle", "git failed")
	res.write("**Error:** " .. tostring(err))
	res.complete_with_error()
	return
end

local summary = git.summary_markdown(data)
local history = git.history_markdown(data)
local diff = git.diff_markdown(data)

local body = table.concat({ "# Git Full Diff", "", summary, "", history, "", diff }, "\n")
if #data.untracked > 0 then
	local untracked = git.untracked_markdown(data)
	body = body .. "\n\n" .. untracked
end

res.write(body)
res.set_structured(git.structured(data))
res.append_data(git.uicontrol(data))
res.set_status("SourceCommit", string.format("+%d −%d · %d files", data.totals.add, data.totals.del, data.totals.files))
res.complete_with_success()
