--[[
name: term
title: Interactive terminal
description: Runs any command in an interactive terminal attached to this message and waits for it to exit. The command is not sent to the model.
category: Chat
generate: false
model-facing: hidden
argument-schema: '<command> [args...]'
]]

-- A generic consumer of process.run_terminal: proves the handle is not Claude-specific.

local msg = dass.command.context.get_message()
if not msg then
	return false
end

local command = command_args.positionals[1]
if command == nil or command == "" then
	msg.error = "Usage: /term <command> [args...]"
	return false
end

local args = command_args.rest
if args == "" then
	args = nil
end

local term, err = process.run_terminal(command, args)
if not term then
	msg.error = err
	return false
end

msg:append_data(term)

local code = await term:wait()
if code ~= 0 then
	msg.error = string.format("%s exited with code %d.", command, code)
	return false
end

return true
