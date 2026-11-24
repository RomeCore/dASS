--[[
title: Log viewer
description: Reports what the application log holds - the count per level for a filter and the newest matching entries. Read-only, because the viewer tab is created and owned by the 'logviewer' script and keeps following new entries on its own; this tool never creates, opens or moves anything on screen. Use it when the user asks what the log says, which errors happened, or what the application is doing.
category: Diagnostics
behaviours: []
argument_schema: >-
  {
    "type": "object",
    "properties": {
      "level": {
        "type": "string",
        "description": "Minimum level to show and count: verbose, debug, info, warning, error or fatal. Default is whatever the viewer is already showing, initially debug."
      },
      "text": {
        "type": "string",
        "description": "Case-sensitive substring of the message or the template."
      },
      "lines": {
        "type": "integer",
        "description": "How many newest matching entries to report back. Default 15, capped at 50."
      }
    },
    "additionalProperties": false
  }
]]

local args = tool_args or {}
local res = dass.tool.result

-- The reader and the tab live in the 'logviewer' script (scripts/lua), shared with the chat.
if logviewer == nil then
	res.write("**Error:** the 'logviewer' Lua script is disabled. Enable it in Settings → Scripts, then run `/addon-reload`.")
	res.complete_with_error()
	return
end

local lines = 15
if type(args.lines) == "number" and args.lines > 0 then
	lines = math.min(50, math.floor(args.lines))
end

-- Read-only: the viewer tab is created and owned by the logviewer script. The arguments filter the
-- report only, never the tab the user is looking at.
local level = type(args.level) == "string" and args.level or nil
local text = type(args.text) == "string" and args.text or nil

local stats = logviewer.stats(level, text)
local tail = logviewer.tail(lines, level, text)

local function cell(text)
	text = tostring(text or ""):gsub("[\r\n]+", " "):gsub("|", "\\|")
	if #text > 160 then text = text:sub(1, 157) .. "..." end
	return text
end

res.use_markdown(true)

local filter = string.format("level >= %s", stats.level)
if stats.text ~= "" then
	filter = filter .. string.format(", text = %q", stats.text)
end

local report = {
	string.format("# Log - %s", filter),
	"",
	string.format("**%d** buffered entries match out of **%d** logged in total.", stats.shown, stats.total),
	"",
	"| Level | Count |",
	"| --- | ---: |",
}
for _, name in ipairs({ "fatal", "error", "warning", "info", "debug", "verbose" }) do
	local count = stats.by_level[name] or 0
	if count > 0 then
		report[#report + 1] = string.format("| %s | %d |", name, count)
	end
end

report[#report + 1] = ""
if #tail == 0 then
	report[#report + 1] = "No entries match the filter."
else
	report[#report + 1] = string.format("Newest %d:", #tail)
	report[#report + 1] = ""
	report[#report + 1] = "| Time | Level | Message |"
	report[#report + 1] = "| --- | --- | --- |"
	for _, entry in ipairs(tail) do
		report[#report + 1] = string.format("| %s | %s | %s |", entry.time, entry.level, cell(entry.message))
	end
end

report[#report + 1] = ""
report[#report + 1] = "The **Logs** tab follows new entries on its own filter; these numbers are the report's."
report[#report + 1] = ""

res.write(table.concat(report, "\n"))
res.set_structured(stats)
res.complete_with_success()
