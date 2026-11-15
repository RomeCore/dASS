--[[
name: hook-trace
description: Logs every hook event it receives, fire-and-forget.
hook-type: "*"
execution-flow: fire-and-forget
]]

local encoded = payload and json.encode(payload) or "null"

dass.log.info("HOOK {Event} target={Target} payload={Payload}",
	hook_type or "?", hook_target or "-", encoded)
