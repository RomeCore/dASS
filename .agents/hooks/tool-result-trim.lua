--[[
name: tool-result-trim
title: Trim oversized tool results
description: Cuts a tool result that is too long for the context down to its head and tail and archives the full text under .llmassist/tool_results/. Tools matching ignore-targets are exempt - a file read is exactly what the caller asked for.
category: Hooks
hook-type: tool.result.*
execution-flow: sequential
timeout: 10000
params-schema:
  folder: { type: 'textbox/string', title: 'Archive folder', default: '.llmassist/tool_results' }
  ignore-targets: { type: 'textbox/string', title: 'Never trim these tools', placeholder: '*.fs-*, mcp.search', default: '*.fs-*' }
  max-chars: { type: 'slider/integer', title: 'Maximum characters left in the prompt', min: 1000, max: 2000000, step: 1000, default: 12000 }
  head-chars: { type: 'slider/integer', title: 'Characters kept from the start', min: 500, max: 500000, step: 500, default: 4000 }
  tail-chars: { type: 'slider/integer', title: 'Characters kept from the end', min: 0, max: 200000, step: 250, default: 1000 }
]]

local settings = params or {}

local function numeric(name, fallback)
	local value = settings[name]
	if type(value) == "number" then return value end
	return fallback
end

-- The chain replaces rather than merges: the effective text is the previous hook's rewrite, if any.
local text = type(previous) == "table" and previous.result or nil
if type(text) ~= "string" then text = payload and payload.result end
if type(text) ~= "string" then return previous end

local limit = math.max(1, numeric("max-chars", 12000))
if #text <= limit then return previous end

local tool = payload and payload.tool

-- The dispatcher injects the origin-qualified name (`native.fs-read`), so the glob dialect of the
-- frontmatter's hook-target works here too, and `*.fs-*` covers every origin at once.
local subject = hook_target
if type(subject) ~= "string" or subject == "" then subject = tostring(tool or "") end

local ignoreTargets = settings["ignore-targets"]
if type(ignoreTargets) ~= "string" or ignoreTargets == "" then ignoreTargets = "*.fs-*" end

for pattern in ignoreTargets:gmatch("[^,;]+") do
	pattern = pattern:match("^%s*(.-)%s*$")
	-- A pattern the dialect does not cover raises; that must not break the hook for every tool.
	local matched, match = pcall(glob.test, pattern, subject, "i")
	if matched and match then return previous end
end

-- Cutting inside a multi-byte sequence puts mojibake into the prompt, so both cuts step off a UTF-8 boundary.
local function align_back(text, index)
	while index > 0 do
		local byte = text:byte(index + 1)
		if byte == nil or byte < 0x80 or byte > 0xBF then return index end
		index = index - 1
	end
	return 0
end

local function align_forward(text, index)
	while index <= #text do
		local byte = text:byte(index)
		if byte < 0x80 or byte > 0xBF then return index end
		index = index + 1
	end
	return #text + 1
end

-- Prefer the last line break, so the kept head ends on a line the model can still parse.
local function head_stop(text, limit)
	local stop = math.min(limit, #text)
	local newline = text:sub(1, stop):find("\r?\n[^\r\n]*$")
	if newline and newline > math.floor(stop / 2) then return newline - 1 end
	return align_back(text, stop)
end

local function tail_start(text, limit)
	if limit <= 0 then return #text + 1 end
	local start = math.max(1, #text - limit + 1)
	local _, newlineEnd = text:find("\r?\n", start)
	if newlineEnd then return newlineEnd + 1 end
	return align_forward(text, start)
end

-- The kept parts together stay under the threshold even when the sliders are set against each other.
local headLimit = math.min(numeric("head-chars", 4000), math.floor(limit * 0.8))
local tailLimit = math.min(numeric("tail-chars", 1000), limit - headLimit)

local headStop = head_stop(text, headLimit)
local tailFrom = tail_start(text, tailLimit)

local head = text:sub(1, headStop)
local tail = ""
local omitted = #text - headStop
if tailFrom > headStop + 1 and tailFrom <= #text then
	tail = text:sub(tailFrom)
	omitted = tailFrom - headStop - 1
end
head = head:gsub("%s+$", "")
tail = tail:gsub("^%s+", "")

local folder = settings.folder
if type(folder) ~= "string" or folder == "" then folder = ".llmassist/tool_results" end

local safeTool = (tostring(tool or "tool"):gsub("[^%w%-_]", "_"))
local status = tostring(payload and payload.status or "unknown")
local stamp = datetime.format(datetime.now_local(), "yyyy-MM-dd_HH-mm-ss-fff")

local function archive_path(index)
	local suffix = index > 1 and ("-" .. index) or ""
	return fs.join(folder, string.format("%s-%s-%s%s.txt", stamp, safeTool, status, suffix))
end

local path = archive_path(1)
local index = 1
while fs.exists(path) do
	index = index + 1
	path = archive_path(index)
end

local saved, failure = pcall(function()
	fs.create_dir(folder)
	fs.write(path, text)
end)
if not saved then
	-- A broken archive must not cost the text as well: leave the result exactly as it came.
	dass.log.warning(
		"tool-result-trim: cannot archive a {Chars}-character result to {Path}: {Error}",
		#text, path, tostring(failure))
	return previous
end

-- One plain line instead of a wrapper: the result keeps looking like what the tool produced.
local marker = string.format(
	"[... tool-result-trim: %d of %d characters omitted - full text saved to %s, read it with fs-read_entry or fs-grep if the middle matters ...]",
	omitted, #text, fs.replace_separators(path, "/"))

local stub = head .. "\n\n" .. marker
if tail ~= "" then stub = stub .. "\n\n" .. tail end

local verdict = {}
if type(previous) == "table" then
	for key, value in pairs(previous) do verdict[key] = value end
end
verdict.result = stub

return verdict
