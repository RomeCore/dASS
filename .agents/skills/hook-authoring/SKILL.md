---
name: hook-authoring
description: Write or amend a dASS hook addon - the .lua / .py / .sh / .ps1 / .bat / .cmd file in a hooks/ addon folder - keeping its frontmatter (hook-type, hook-target, execution-flow, timeout) true to the event it targets and the verdict its body returns true to what the pipeline does with it. Use when creating a hook, when editing or debugging one, when a hook never fires, when a guard silently allows what it should block or approves what the user denied, or when the user asks how a hook addon is shaped.
---

# Hook authoring (dASS)

A hook is a single file in a `hooks/` folder. Its frontmatter says **which point of the tool pipeline it runs at**; its body returns a **verdict** the pipeline obeys. The body is not callable, not a tool, not a command.

There are three points: `tool.start`, `tool.confirm` and `tool.result`. Their event names spell the invocation source, and two of them also spell the outcome or the pending decision.

Two failure modes dominate, and neither raises:

- a `hook-type` or `hook-target` that matches nothing - the hook exists, loads and is never called;
- a guard that answers "no opinion" by accident - an empty return, a typo in `decision`, or a timeout.

A third belongs to `tool.confirm`: a verdict that tries to lift a denial without naming the layer it pierces. It is accepted and ignored, and the call stays denied.

Every run of this skill ends with the file proven to fire, or with "not verified yet" said out loud.

## Steps

### 1. Find the file, or establish that it should exist

List the `hooks/` folders of the addon sources and grep for the name. Hooks are **short format only**: `hooks/<name>.<ext>`, never a folder.

*Done when:* the path is known, or the hook is established as new.

### 2. Read the contract before the body

`hook-type` says the moment, `hook-target` says the tool, `execution-flow` says whether anything waits for the answer. Then read the body's return against the type's page.

*Done when:* the event in the frontmatter and the fields in the returned document agree with **Event types**.

### 3. Change the hook

- the moment changes → `hook-type` moves with it
- the tool the hook is about changes → `hook-target` moves with it
- a long body appears → `timeout` moves with it (the default is 2 seconds)

*Done when:* the body's return is a valid verdict, and the frontmatter still names the right event.

### 4. Verify as far as the situation deserves

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses and the name matches the file |
| Trigger the event | the hook loads **and** is matched - addons hot-reload on their own (an edit through an addon tool, app activation, an external script run), so no reload step is needed |
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
hook-target: "native.fs-*"
execution-flow: sequential
timeout: 2000
]]
```

| Key | Default | Meaning |
|---|---|---|
| `hook-type` | **required** | anchored, case-insensitive glob over the event name |
| `hook-target` | all tools | anchored, case-insensitive glob over `{origin}.{name}` |
| `execution-flow` | caller's default (sequential at all three tool points) | `sequential`, `parallel`, `fire-and-forget` |
| `timeout` | awaited: 2000, capped at 30000; fire-and-forget: none unless declared | milliseconds an awaited hook gets before it is cancelled |
| `json-output` | - | **required on every out-of-process engine**, refused on Lua |
| `json-input` | `auto` | `true` hands the payload over as JSON text, `auto` as the native structure |
| `enabled`, `order`, `aliases` | - | shared by every addon kind |

`run-terminal` is refused for hooks. `hidden` is parsed but means nothing here: a hook is either enabled or not. Only a sequential hook can affect a call - a `fire-and-forget` or `parallel` hook gets no `previous` and its verdict is dropped.

## Event types

| Type | When it runs | Verdict |
|---|---|---|
| `tool.start.{source}` | before validation and the lock; may refuse the call or rewrite its arguments | `decision`, `reason`, `arguments`, `persists_arguments` - [types/tool.start.md](types/tool.start.md) |
| `tool.confirm.{source}.{pending}` | inside the lock, after policy and before any approval surface; may answer in the user's place | `decision`, `reason`, `arguments`, `persists_arguments`, `override` - [types/tool.confirm.md](types/tool.confirm.md) |
| `tool.result.{source}.{status}` | after settlement, when the text is final; may rewrite what the model reads | `result`, `markdown` - [types/tool.result.md](types/tool.result.md) |

### The axes

| Axis | Values | Meaning |
|---|---|---|
| `source` (event suffix) | `chat-run`, `chat-restart`, `command`, `agent-task`, `script` | who launched the call - an agent, the user, a task, a script |
| `{origin}` (name prefix) | `native`, `script`, `mcp`, `ad-hoc`, `unknown` | where the tool comes from; `unknown` is a tool that does not exist |
| `{status}` (result suffix) | `success`, `error`, `cancelled`, `rejected` | the terminal status of the call |
| `{pending}` (confirm suffix) | `approve`, `ask`, `disallow` | the decision the policy stage arrived with |

Not every source reaches every point, because the orchestrators differ:

| Source | `tool.start` | `tool.confirm` | `tool.result` |
|---|---|---|---|
| `chat-run` | yes | yes | yes |
| `agent-task` | yes | yes | yes |
| `chat-restart` | yes | - | yes |
| `command` | yes | - | yes |
| `script` | yes | - | yes |

The confirm point exists only where policy and confirmation run. A user's own re-run and a `/tool:<name>` call skip both; a script call skips policy, confirmation, the hard floor and the synchronization lock, but it **is** hooked at start and result.

The payload's `tool` field is the **bare** name (`fs-edit`), while `hook-target` and the `hook_target` global are origin-qualified (`native.fs-edit`). A tool that does not exist matches `unknown.<name>`, and its payload still carries the emitted name in `tool`.

So `tool.start.chat-run` + `native.fs-edit` is an agent running a native tool, `tool.start.command` + `script.*` is the user running a scripted one by hand, and `tool.result.script.error` is any tool that a Lua script called and that failed.

### Decision words

`decision` is shared by the start and the confirm points, and it does not mean the same thing there:

| Word | At `tool.start` | At `tool.confirm` |
|---|---|---|
| `none` / absent | no opinion | no opinion, the pending decision is kept |
| `allow` | no objection | approve in the user's place: the user is not asked |
| `ask` | no objection | ask the user (or the tool's own approval surface) |
| `disallow` | refusal, status `Rejected` | final refusal, status `Rejected` |
| `error` | refusal, status `Error` | final refusal, status `Error` |

## One example

```lua
--[[
name: secret-guard
description: Denies any filesystem tool call whose arguments name a secret file.
hook-type: tool.start.*
hook-target: "native.fs-*"
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
| Python, Bash, PowerShell, Batch | `"""` / `#---` / `REM ---` | `json-output: true`, Desktop build | a structure, or JSON text with `json-input: true`; Bash and Batch always get JSON text |

Lua is the default: every build has it, it runs in-process and hands the verdict back natively. An out-of-process hook must declare `json-output: true` - without it the addon fails to load with `InvalidKind`, because a hook always answers with a structure. In an out-of-process hook the variables are `payload`, `previous`, `hook_type` and `hook_target`; exiting with code **2** blocks the chain with the error text as the message, which the start and confirm points turn into a refusal and the result point only logs.

A Lua hook runs in the chat's Lua state, so the `dass.*` APIs are there. A hook that calls a tool through them re-enters the pipeline as the `script` source: no policy, no confirmation, no lock - but the start and result points fire again, so a hook that matches `tool.start.*` and calls a tool can re-enter itself. Keep a hook that calls tools narrow in `hook-type`, or do not call tools from it.

## What bites

- A bare family name - `tool.start`, `tool.result`, `fs-*` - matches **nothing**: globs are anchored. Write `tool.start.*`, `native.fs-*`.
- A typo in an outcome or decision name (`tool.result.*.sucess`, `decision = "deny"`) is a hook that never fires: nothing validates `hook-type`, and an unknown decision word becomes "no opinion".
- The chain **replaces**, it does not merge: a hook with nothing to add must `return previous`, not `return {}`.
- A timeout, a crash or an unparseable return is "no opinion". A guard that fails lets the call through.
- `json-output` on a Lua hook: refused at load, Lua has no stdout channel.
- At `tool.confirm`, `allow` skips the user. Turning a pending `ask` into an approval is the point; lifting a pending `disallow` needs `override`, and it defaults to `policy`, which cannot pierce a memorized "no" or a tool's `AlwaysDisallow`.
- The confirm hook runs for every decision, including the ones already approved or refused. Target one case with the `{pending}` suffix instead of branching in the body.
- The payload's `tool` is bare. A guard that keys off the origin must use the `hook-target` glob or the `hook_target` global.

## Neighbours

- User-editable hook settings (`params-schema`, the card editor, `params` in the body): the `addon-parameterization` skill.
- The same engine table for other scriptable kinds: the `tool-authoring`, `command-authoring` and `context-authoring` skills.
