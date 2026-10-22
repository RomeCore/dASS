--[[
name: git-status
kind: supersede
description: Branch, upstream tracking and the working-tree changes of the current repository, grouped into conflict / staged / unstaged / untracked. Re-announces only when the rendered state changes.
timeout: 5000
]]

-- The provider runs this body on every prompt build to compare it byte-exactly against the
-- previous stamp, so it must stay one cheap `git status` call with bounded output. A new
-- stamp appears only when the text below actually changes, and the old one stops being
-- injected - so the lines are worth being accurate rather than terse.
local MAX_ENTRIES = 40

if not process.which("git") then
	return
end

local r = await process.exec("git", {
	"--no-pager", "--no-optional-locks",
	"-c", "core.quotepath=false",
	"status", "--porcelain=v1", "--branch",
}, { timeout_seconds = 3 })

if r.exit_code ~= 0 or r.timed_out then
	return
end

local lines = {}
for line in string.gmatch(r.stdout, "([^\r\n]+)") do
	lines[#lines + 1] = line
end

-- ---- head line: branch, upstream, ahead/behind ------------------------------

local function describe_head(s)
	if s == "" then
		return "branch: (unknown)"
	end

	local core, flags = string.match(s, "^(.-)%s*%[(.*)%]$")
	core = core or s
	flags = flags or ""

	local branch, upstream = string.match(core, "^(.-)%.%.%.(.+)$")
	if not branch then
		branch, upstream = core, nil
	end

	local bits = {}
	if upstream then
		bits[#bits + 1] = "-> " .. upstream
	end
	local ahead = tonumber(string.match(flags, "ahead (%d+)"))
	local behind = tonumber(string.match(flags, "behind (%d+)"))
	if ahead then bits[#bits + 1] = "ahead " .. ahead end
	if behind then bits[#bits + 1] = "behind " .. behind end
	if string.find(flags, "gone", 1, true) then bits[#bits + 1] = "upstream gone" end

	local out = "branch: " .. branch
	if #bits > 0 then
		out = out .. " (" .. table.concat(bits, ", ") .. ")"
	end
	return out
end

if lines[1] and string.sub(lines[1], 1, 3) == "## " then
	print(describe_head(string.sub(lines[1], 4)))
	table.remove(lines, 1)
end

-- ---- classify every entry of `git status --porcelain=v1 ---------------------

local conflicts, staged, unstaged, untracked = {}, {}, {}, {}

for _, line in ipairs(lines) do
	if #line > 3 then
		local x, y = string.sub(line, 1, 1), string.sub(line, 2, 2)
		local path = string.sub(line, 4)

		if x == "U" or y == "U" or x .. y == "AA" or x .. y == "DD" then
			conflicts[#conflicts + 1] = { mark = x .. y, path = path }
		elseif x == "?" and y == "?" then
			untracked[#untracked + 1] = { mark = "", path = path }
		else
			if x ~= " " and x ~= "?" then
				staged[#staged + 1] = { mark = x, path = path }
			end
			if y ~= " " and y ~= "?" then
				unstaged[#unstaged + 1] = { mark = y, path = path }
			end
		end
	end
end

-- ---- summary: counts, then the one question an agent actually asks ----------

local parts = {}
local function add_part(n, word)
	if n > 0 then
		parts[#parts + 1] = n .. " " .. word
	end
end
add_part(#conflicts, "conflicted")
add_part(#staged, "staged")
add_part(#unstaged, "unstaged")
add_part(#untracked, "untracked")

if #parts == 0 then
	print("changes: none - working tree is clean")
else
	print("changes: " .. table.concat(parts, ", "))

	if #conflicts > 0 then
		print("next commit: blocked - " .. #conflicts .. " conflicted " .. (#conflicts == 1 and "path" or "paths") .. " must be resolved first")
	elseif #staged > 0 then
		print("next commit: " .. #staged .. " staged " .. (#staged == 1 and "file" or "files") .. " would go in, worktree-only changes would not")
	else
		print("next commit: nothing is staged - a commit now would be empty until `git add`")
	end
end

-- ---- groups, conflicts first, under one shared entry budget ----------------

local shown = 0
local function dump(title, items)
	if #items == 0 then return end

	print("")
	print(title .. " (" .. #items .. "):")

	local room = MAX_ENTRIES - shown
	local n = math.min(#items, room)
	for i = 1, n do
		local e = items[i]
		local mark = e.mark ~= "" and (e.mark .. " ") or ""
		print("  " .. mark .. e.path)
	end
	shown = shown + n

	if n < #items then
		print("  ... and " .. (#items - n) .. " more (`git status --short`)")
	end
end

dump("conflicts, unmerged", conflicts)
dump("staged, index vs HEAD", staged)
dump("unstaged, worktree vs index", unstaged)
dump("untracked", untracked)
