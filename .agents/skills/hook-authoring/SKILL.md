---
name: hook-authoring
description: Write or amend a dASS hook addon - the .lua / .py / .sh / .ps1 / .bat / .cmd file in a hooks/ addon folder - keeping its frontmatter (hook-type, hook-name, execution-flow, timeout) true to the event it targets and the verdict its body returns true to what the pipeline does with it. Use when creating a hook, when editing or debugging one, when a hook never fires, when a guard silently allows what it should block, or when the user asks how a hook addon is shaped.
---

# Hook authoring (dASS)

A hook is a single file in a `hooks/` folder. Its frontmatter says **which point of the pipeline it runs at**; its body returns a **verdict** the pipeline obeys. The body is not callable, not a tool, not a command.

Two failure modes dominate, and neither raises:

- a `hook-type` or `hook-name` that matches nothing - the hook exists, loads and is never called;
- a guard that answers "no opinion" by accident - an empty return, a typo in `decision`, or a timeout.

Every run of this skill ends with the file proven to fire, or with "not verified yet" said out loud.

## Steps

### 1. Find the file, or establish that it should exist

List the `hooks/` folders of the addon sources and grep for the name. Hooks are **short format only**: `hooks/<name>.<ext>`, never a folder.

*Done when:* the path is known, or the hook is established as new.

### 2. Read the contract before the body

`hook-type` says the moment, `hook-name` says the tool, `execution-flow` says whether anything waits for the answer. Then read the body's return against the type's page.

*Done when:* the event in the frontmatter and the fields in the returned document agree with **Event types**.

### 3. Change the hook

- the moment changes → `hook-type` moves with it
- the tool the hook is about changes → `hook-name` moves with it
- a long body appears → `timeout` moves with it (the default is 2 seconds)

*Done when:* the body's return is a valid verdict, and the frontmatter still names the right event.

### 4. Verify as far as the situation deserves

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses and the name matches the file |
| `/addon-reload`, then trigger the event | the hook loads **and** is matched |
| Watch the result | the verdict was applied, not just returned |

The failure mode worth naming: a hook with a wrong `hook-type` or a bare family name loads perfectly and does nothing. The proof is a call that behaves differently, not a file on disk.

*Done when:* the level of checking is stated, together with what it does not prove.

### 5. Show the result

The path, plus what was verified and by which move.

## Where the file goes

`hooks/<name>.<ext>` inside an addon source: `.agents/` in a repo, `.llmassist/packs/<pack>/`, `%LOCALAPPDATA%\.llmassist\`. One file, never a folder.

## The frontmatter

```yaml
--[[
name: secret-guard
description: Denies any filesystem tool call whose arguments name a secret file.
hook-type: tool.start.*
hook-name: "native.fs-*"
execution-flow: sequential
timeout: 2000
]]
```

| Key | Default | Meaning |
|---|---|---|
| `hook-type` | **required** | anchored glob over the event name |
| `hook-name` | all tools | anchored glob over `{origin}.{name}` |
| `execution-flow` | caller's default (sequential at both tool points) | `sequential`, `parallel`, `fire-and-forget` |
| `timeout` | 2000, ceiling 10000 | milliseconds an awaited hook gets; a fire-and-forget hook has no ceiling |
| `json-output` | - | **required on every out-of-process engine**, refused on Lua |
| `json-input` | `auto` | `true` hands the payload over as JSON text, `auto` as the native structure |
| `enabled`, `hidden`, `order`, `aliases` | - | shared by every addon kind |

`run-terminal` is refused for hooks. A `fire-and-forget` or `parallel` hook can never affect a call: nothing receives its verdict.

## Event types

| Type | When it runs | Verdict |
|---|---|---|
| `tool.start.{source}` | before validation and the lock; may refuse the call or rewrite its arguments | `decision`, `reason`, `arguments`, `persists_arguments` - [types/tool.start.md](types/tool.start.md) |
| `tool.result.{source}.{status}` | after settlement, when the text is final; may rewrite what the model reads | `result`, `markdown` - [types/tool.result.md](types/tool.result.md) |

Two axes are spelled in the name and are easy to confuse:

| Axis | Values | Meaning |
|---|---|---|
| `source` (event suffix) | `chat-run`, `chat-restart`, `command`, `agent-task`, `script` | who launched the call - an agent, the user, a task |
| `{origin}` (name prefix) | `native`, `script`, `mcp`, `ad-hoc` | where the tool comes from |

So `tool.start.chat-run` + `native.fs-edit` is an agent running a native tool, and `tool.start.command` + `script.*` is the user running a scripted one by hand.

## One example

```lua
--[[
name: secret-guard
description: Denies any filesystem tool call whose arguments name a secret file.
hook-type: tool.start.*
hook-name: "native.fs-*"
execution-flow: sequential
timeout: 2000
]]

local markers = { { "%.env$", "environment file" }, { "%.pem$", "PEM key" }, { "%.key$", "private key" } }
local templates = { "%.env%.example$", "%.env%.sample$" }

local function is_template(text)
	for _, pattern in ipairs(templates) do
		if text:find(pattern) then return true end
	end
	return false
end

-- Every string in the arguments is a candidate: a path can arrive as `path`, as the `pattern`
-- of fs-grep or inside the body of fs-write_file.
local candidates = {}
local function collect(node)
	if type(node) == "string" then
		candidates[#candidates + 1] = node
	elseif type(node) == "table" then
		for _, value in pairs(node) do collect(value) end
	end
end

collect(payload and payload.arguments)

for _, text in ipairs(candidates) do
	local lowered = text:lower()
	if not is_template(lowered) then
		for _, marker in ipairs(markers) do
			if lowered:find(marker[1]) then
				return {
					decision = "disallow",
					reason = string.format(
						"Blocked by the secret-guard hook: the %s call would touch a secret (%s: %s).",
						payload.tool or "tool", marker[2], text:sub(1, 120)),
				}
			end
		end
	end
end

return previous
```

## Languages

| Engine | Frontmatter | Needs | Payload arrives as |
|---|---|---|---|
| Lua | `--[[ ]]` | nothing, in-process | a table (`payload.tool`) |
| Python, Bash, PowerShell, Batch | `"""` / `#---` / `REM ---` | `json-output: true`, Desktop build | a structure, or JSON text with `json-input: true` |

Lua is the default: every build has it, it runs in-process and hands the verdict back natively. An out-of-process hook must declare `json-output: true` - without it the addon fails to load with `InvalidKind`, because a hook always answers with a structure.

A Lua hook runs in the chat's Lua state, so the `dass.*` APIs are there. A hook that calls a tool through them re-enters the pipeline through the script orchestrator, which is not hooked and takes no lock: no recursion, but no policy either.

## What bites

- A bare family name - `tool.start`, `tool.result`, `fs-*` - matches **nothing**: globs are anchored. Write `tool.start.*`, `native.fs-*`.
- A typo in an outcome name (`tool.result.*.sucess`) is a hook that never fires: nothing validates `hook-type`.
- The chain **replaces**, it does not merge: a hook with nothing to add must `return previous`, not `return {}`.
- A timeout, a crash or an unparseable return is "no opinion". A guard that fails lets the call through.
- An unknown `decision` word fails deserialization and becomes "no opinion" - the same silent allow.
- `json-output` on a Lua hook: refused at load, Lua has no stdout channel.
