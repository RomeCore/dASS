---
name: hook-authoring
description: Write or amend a dASS hook addon - the .lua / .py / .sh / .ps1 / .bat / .cmd file in a hooks/ addon folder - keeping its frontmatter (hook-type, hook-target, execution-flow, timeout) true to the tool or chat event it targets and the verdict its body returns true to what the pipeline does with it. Use when creating a hook, when editing or debugging one, when a hook never fires, when a guard silently allows what it should block or approves what the user denied, or when the user asks how a hook addon is shaped.
---

# Hook authoring (dASS)

A hook is a single file in a `hooks/` folder. Its frontmatter says **which pipeline and which point of it** the hook runs at; its body returns a **verdict** the pipeline obeys. The body is not callable, not a tool, not a command.

Three pipelines are hooked today. The **tool pipeline** has three points - `tool.start`, `tool.confirm` and `tool.result` - whose event names spell the invocation source, two of which also spell the outcome or the pending decision. The **chat pipeline** has the user-message points (`message.user.submit.*`, `message.user.insert.*`), the assistant-message points (`message.assistant.start`, `message.assistant.stop.*`), the turn points (`chat.turn.*`) and the two session points (`session.start`, `session.end`). The **task pipeline** has two points - `task.start.{source}.{new,continue}` and `task.stop.{source}.{success,error,cancelled,blocked}` - around one launch of an agentic task, and only a task that was given a chat fires them at all.

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
| `hook-target` | all targets | anchored, case-insensitive glob over `{origin}.{name}` for a tool, `{sender id}.{sender name}` for a user message, `{agent id}.{agent name}` for an agent event, `{source id}.{task name}` for a task event; the turn-level points have no target |
| `execution-flow` | caller's default: sequential at the tool and task-start points, fire-and-forget at the session and task-stop points | `sequential`, `parallel`, `fire-and-forget` |
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

## Chat events

The user-message points run in the chat pipeline, around the message the user sends on its way into the history.

| Type | When it runs | Verdict |
|---|---|---|
| `message.user.submit.{direct,queue,edit}` | in the send pre-flight, before the draft is committed and before anything is written; may refuse the send | `decision`, `reason` - [types/message.user.submit.md](types/message.user.submit.md) |
| `message.user.insert.{pre,post}` | around the write of the message; `pre` may refuse it, `post` has already happened | `pre`: `decision`, `reason`; `post`: none - [types/message.user.insert.md](types/message.user.insert.md) |
| `message.assistant.start` | the pending assistant message exists, the request is not assembled yet; parallel (waited for, result dropped) | none - [types/message.assistant.start.md](types/message.assistant.start.md) |
| `message.assistant.stop.{success,error,cancelled}` | the message reached a terminal status | none - [types/message.assistant.stop.md](types/message.assistant.stop.md) |
| `chat.turn.begin` | the multi-agent turn is about to start, before any agent is selected; may refuse the whole turn | `decision`, `reason` - [types/chat.turn.begin.md](types/chat.turn.begin.md) |
| `chat.turn.agent.begin` | the orderer has to pick the next agent; may pick another one or end the turn | `decision`, `agent`, `reason` - [types/chat.turn.agent.begin.md](types/chat.turn.agent.begin.md) |
| `chat.turn.agent.continue` | an agent runs again without a fresh selection: a resume or a tool cycle | none - [types/chat.turn.agent.continue.md](types/chat.turn.agent.continue.md) |
| `chat.turn.agent.stop.{status}.{reason}` | one agent's turn is over; the last variant is `complete` or `interrupted` | none - [types/chat.turn.agent.stop.md](types/chat.turn.agent.stop.md) |
| `chat.turn.stop.{reason}` | the multi-agent turn is over, on every exit path; `complete`, `interrupted` or `cancelled` | none - [types/chat.turn.stop.md](types/chat.turn.stop.md) |
| `session.start` | a chat has just been opened: every chat service is constructed and the history is loaded | none - [types/session.start.md](types/session.start.md) |
| `session.end` | the chat is closing - another chat, a closed tab or the application - and its services are still alive for one call | none - [types/session.end.md](types/session.end.md) |

The `submit` variant names the action the user took, not the path the message then took: a `direct` send joins the follow-up queue when one is already pending, and stays `direct`. `edit` is a commit of an edited message.

The target of every `message.user.*` event is `{sender id}.{sender name}` - `admin.Admin`, or the bare login when the name does not resolve - so `admin.*`, `*.Admin` and `admin.Admin` all match.

A UI send fires `submit` and then `insert.pre`, and both can refuse it. Which one a guard belongs on is a decision, not a style: only `submit` refuses before the draft is cleared.

The target of an assistant event is `{agent id}.{agent name}`. While an assistant message is being generated, `dass.chat.current_message()` returns that message as a `chat_message` handle - the same one `get_messages()` would - so a hook can read and write it directly instead of through a verdict.

The two session points have no target and no verdict, and both force fire-and-forget: `execution-flow` is ignored there and nothing waits for the hook. `session.start` is one opening of a chat, not one run of the application - reopening the same chat fires it again with a new `session_id` and the same `chat_id`.

## Task events

An agentic task is a background run of one agent: `agent-call`, `agent-callsub`, chat naming, summarization, memory recording and retrieval, a `/agent:<name>` command, a Lua `dass.agent.call`. The task pipeline hooks one **launch** of one task - an `Execute` or a `Continue` - and nothing else.

| Type | When it runs | Verdict |
|---|---|---|
| `task.start.{source}.{new,continue}` | the task is `Executing` and its first request has not run yet; may block the launch | `decision`, `reason` - [types/task.start.md](types/task.start.md) |
| `task.stop.{source}.{success,error,cancelled,blocked}` | the launch reached a terminal status, after the awaiting caller was already resumed | none - [types/task.stop.md](types/task.stop.md) |

| Axis | Values | Meaning |
|---|---|---|
| `{source}` | `internal`, `tool`, `script`, `manual`, `unknown` | the launcher kind |
| `{entry}` | `new`, `continue` | a fresh task or a relaunch of a finished one |
| `{status}` | `success`, `error`, `cancelled`, `blocked` | the terminal status; `blocked` is a launch `task.start` refused |

The target is `{source id}.{task name}`: `agent-callsub.web-searcher`, `chat-naming.unknown`. The source id is free-form and belongs to the launcher - `chat-agent-call`, `agent-callsub`, `lua-api`, `command`, `chat-naming` - so narrow a hook by the id and read the name in the body. A task with no name contributes `unknown`, never an empty segment.

**Only a task with a chat fires these events.** The dispatcher is a chat service, so a task launched with no chat has no hook surface at all, and one whose chat was closed while it ran has none left at `stop`.

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
- A `message.user.*` hook fires **twice** for a UI send - at `submit` and again at `insert.pre`. Subscribe a guard to one point, not to the family.
- A refusal at `message.user.insert.pre` cannot keep the user's text: the draft is already committed. Only `message.user.submit.*` refuses before that point.
- The `submit` payload carries the text as typed. A guard that keys on `/tool:...` sees the token, and the slash command has not run yet - it may rewrite the message afterwards.
- `message.user.insert.post` has no verdict. Its return value is dropped, and the hook's own writes may race with the next message.
- `chat.turn.agent.begin` does **not** fire every cycle: a forced agent and an agent continuing its own tool batch skip it. The continuation is reported by `chat.turn.agent.continue` instead.
- A refusal at `chat.turn.agent.begin` ends the turn, and at the first cycle the user sees the generic "agent selection failed" warning. Refuse the turn at `chat.turn.begin` when the reason has to reach the user.
- An agent name that does not resolve at `chat.turn.agent.begin` is a warning and "no opinion", not a refusal: the stages choose as if the hook had said nothing.
- One turn call, one `chat.turn.stop`. A follow-up barrier ends one turn and starts the next, so two answers in a row produce two stop events.
- `chat.turn.agent.stop` fires per agent run, not per message: a run with three tool batches is one agent stop and four message stops.
- `session.end` runs on the closing path: the chat scope is disposed right after the handler returns, so a hook that still has to resolve a chat service, start a turn or await anything may find the chat gone. Write with what is already in hand (`store.session`, files) and return fast. A crash or a process kill runs nothing.
- A session point is not "once per application run": every opening fires `session.start`, temporary chats included, and a hook registered at run time from a script is already there when it fires.
- `task.stop.*.error` is `AgentTaskStatus.Failed`: the word does not follow the enum name, and `failed` matches nothing.
- `task.start` is a gate as much as an event. Only a sequential hook's verdict is read, so `execution-flow: fire-and-forget` or `parallel` gives up the power to block; an out-of-process hook exiting with code 2 blocks regardless.
- `task.start` fires per launch, not per task. `agent-call` followed by `agent-task_continue` on the finished task is two starts and two stops with the same `task_id`, the second suffixed `continue`.
- A follow-up pushed into a **running** task is not a launch and fires nothing; the `continue` events belong to a relaunch of a finished one.
- A blocked launch reports `status: blocked` and `block_reason`, not `error`. The caller that waited sees the reason as an exception, the one that did not wait reads it off the task.
- Internal tasks fire too - `chat-naming`, `chat-summarization`, `memory-recording`, `memory-retrieval`, `chat-agent-selection` - so `task.start.internal.new` runs on the first message of every chat. Narrow it with `hook-target`.
- `task.stop` is forced fire-and-forget: `execution-flow` is ignored, so stop hooks are not ordered among themselves and a verdict goes nowhere. An undeclared `timeout` is not bounded either.
- `local payload = payload or {}` **empties the payload**: the Lua of this runtime reads the new local instead of the global, so the whole body sees an empty table and every field is `nil`, silently. Bind it under another name - `local p = payload or {}`.

## Neighbours

- User-editable hook settings (`params-schema`, the card editor, `params` in the body): the `addon-parameterization` skill.
- The same engine table for other scriptable kinds: the `tool-authoring`, `command-authoring` and `context-authoring` skills.
