--[[
title: Chat LLM cost
description: Reports what the current chat has spent on models - the total in USD, the split by token type (cache hit, cache miss, output) with each type's share and cost, and the per-model total, plus how many messages and agentic task launches were counted. Read-only - it never changes the ledger or the prices. Use it when the user asks what this chat costs, how many tokens were spent, or where the money went; pass `hours` to limit the report to a recent window.
category: General
behaviours: []
argument_schema: >-
  {
    "type": "object",
    "properties": {
      "hours": {
        "type": "integer",
        "description": "Only count entries from the last N hours. Omit or pass 0 for the whole chat."
      }
    },
    "additionalProperties": false
  }
]]

local args = tool_args or {}
local res = dass.tool.result

-- The aggregation lives in the 'llmcost' script (scripts/lua), shared with the overlay.
if llmcost == nil then
	res.write("**Error:** the 'llmcost' Lua script is disabled. Enable it in Settings → Scripts, then run `/addon-reload`.")
	res.complete_with_error()
	return
end

-- A missing field arrives as a null value, not nil, so check the type.
local hours = 0
if type(args.hours) == "number" and args.hours > 0 then hours = math.floor(args.hours) end

local from = nil
if hours > 0 then from = time.now() - hours * 3600 end

local stats = llmcost.stats(from)
local function money(value)
	if value <= 0 then return "$0" end
	if value >= 1 then return string.format("$%.2f", value) end
	if value >= 0.01 then return string.format("$%.4f", value) end
	return string.format("$%.6f", value)
end

res.use_markdown(true)
res.set_status("AttachMoney", "Chat spend")

local window = hours > 0 and string.format("last %d h", hours) or "whole chat"
local lines = {
	string.format("# Chat spend - %s", window),
	"",
	string.format("**%s** across %d messages and %d task launches.",
		money(stats.total), stats.messages, stats.tasks),
	"",
	"| Token type | Tokens | Share | Cost |",
	"| --- | ---: | ---: | ---: |",
}
for _, row in ipairs(stats.breakdown) do
	lines[#lines + 1] = string.format("| %s | %s | %s | %s |",
		row.name, row.detail, row.share, row.cost)
end
lines[#lines + 1] = ""
lines[#lines + 1] = "| Model | Calls | Cost |"
lines[#lines + 1] = "| --- | ---: | ---: |"
for _, row in ipairs(stats.models) do
	lines[#lines + 1] = string.format("| %s | %s | %s |", row.name, row.detail, row.cost)
end
if stats.unpriced > 0 then
	lines[#lines + 1] = ""
	lines[#lines + 1] = string.format(
		"%d entries carry a model with no price in the table: their tokens are counted, their cost is not.",
		stats.unpriced)
end

-- A blank line after the last table: without it Markdown glues whatever the result view
-- appends right after onto the last row.
lines[#lines + 1] = ""
lines[#lines + 1] = ""

res.write(table.concat(lines, "\n"))
res.set_structured({
	total_usd = stats.total,
	window_hours = hours,
	messages = stats.messages,
	tasks = stats.tasks,
	unpriced_entries = stats.unpriced,
	tokens = {
		cache_hit = stats.hit,
		cache_miss = stats.miss,
		output = stats.out,
	},
	breakdown = stats.breakdown,
	models = stats.models,
})
res.complete_with_success()
