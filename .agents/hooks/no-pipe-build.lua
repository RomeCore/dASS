--[[
name: no-pipe-build
title: No pipe in terminal builds
description: Strips any piping from a project build that runs in the embedded terminal - a filtered build log loses live streaming and comes back as mojibake.
category: Hooks
hook-type: tool.start.*
hook-target: "native.shell-*"
execution-flow: sequential
timeout: 2000
]]

-- Building is the one case where a pipe costs more than it saves: filtering the log kills the
-- live streaming and breaks the console encoding, so the summary line nobody can read.
local buildVerbs = {
	"dotnet%s+build",
	"dotnet%s+restore",
	"dotnet%s+publish",
	"dotnet%s+pack",
	"dotnet%s+test",
	"dotnet%s+msbuild",
	"msbuild",
	"npm%s+run%s+build",
	"npm%s+build",
	"yarn%s+build",
	"pnpm%s+build",
	"cargo%s+build",
	"go%s+build",
	"vite%s+build",
}

local function isBuild(command)
	local lowered = command:lower()
	for _, pattern in ipairs(buildVerbs) do
		if lowered:find(pattern) then return true end
	end
	return false
end

-- Cuts at the first real pipe: one inside a quoted string is part of an argument, and `||` is a
-- command separator, not a pipe.
local function stripPipe(command)
	local quote = nil
	for index = 1, #command do
		local char = command:sub(index, index)
		if quote then
			if char == quote then quote = nil end
		elseif char == '"' or char == "'" then
			quote = char
		elseif char == "|" then
			local before = command:sub(index - 1, index - 1)
			local after = command:sub(index + 1, index + 1)
			if before ~= "|" and after ~= "|" then
				local head = command:sub(1, index - 1):gsub("%s+$", "")
				return head, command:sub(index):gsub("^|", ""):gsub("^%s+", "")
			end
		end
	end
	return nil
end

local arguments = payload and payload.arguments
if type(arguments) ~= "table" or type(arguments.command) ~= "string" then return previous end

-- A build outside the terminal may filter its output on purpose: only the terminal needs the rule.
if arguments.runTerminal ~= true then return previous end
if not isBuild(arguments.command) then return previous end

local head, tail = stripPipe(arguments.command)
if not head then return previous end

local rewritten = {}
for key, value in pairs(arguments) do rewritten[key] = value end
rewritten.command = head

return {
	decision = "allow",
	reason = string.format(
		"Removed the pipe from the terminal build (was piping into %s). A filtered build log loses live streaming and turns the summary into mojibake.",
		tail:sub(1, 120)),
	arguments = rewritten,
	persists_arguments = true,
}
