--[[
name: llmcost-message
title: Cost tracker - assistant messages
description: Records the token usage of every assistant message of this chat into its cost ledger.
category: Hooks
hook-type: message.assistant.stop.*
execution-flow: fire-and-forget
]]

-- The namespace may be missing: a script can be switched off by the user.
if llmcost == nil then return end

llmcost.record_message(payload)
