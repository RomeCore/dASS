--[[
name: claude
title: Claude Code
description: Runs Claude Code as a full interactive TUI in a terminal attached to this message and waits for it to exit. Anything typed after the command is seeded into the terminal as the first input. The model answers after Claude Code closes; cancelling the terminal kills the session and stops the answer.
category: Chat
generate: true
model-facing: neutral
argument-schema: '[prompt...]'
]]

-- Claude Code inside dASS: process.run_terminal raises a PTY, the handle is attached to
-- this very message, and the command waits for the TUI to exit. The client is expected to
-- be installed and configured already; this command only launches it.

local msg = dass.command.context.get_message()
if not msg then
	return false
end

-- Spawn first: a PATH probe lies when the process PATH is trimmed (cmd.exe is the
-- classic case), so only diagnose after the spawn has actually failed.
local term, err = process.run_terminal("claude")
if not term then
	if process.which("claude") == nil then
		msg.error = "Claude Code is not on PATH."
	else
		msg.error = err
	end
	return false
end

msg:append_data(term)

local prompt = command_args.rest
if prompt ~= nil and prompt ~= "" then
	-- The PTY buffers input, so seeding right after the spawn is safe: the TUI reads it
	-- as soon as it is ready.
	await term:write(prompt)
	await term:write_line("")
end

try
	local code = await term:wait()
	if code ~= 0 then
		msg.error = string.format("Claude Code exited with code %d.", code)
		return false
	end
catch _ do
	-- skip
end

return true
