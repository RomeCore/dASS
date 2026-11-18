--[[
name: llmcost-task
title: Cost tracker - agentic tasks
description: Records the token usage of every agentic task launch of this chat into its cost ledger.
category: Hooks
hook-type: task.stop.*.*
execution-flow: fire-and-forget
]]

-- The namespace may be missing: a script can be switched off by the user.
if llmcost == nil then return end

llmcost.record_task(payload)
