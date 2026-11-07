# tool.start.{source}

Fires **before** the arguments are validated and before the synchronization keys are derived from them. The only point at which a hook may rewrite arguments: everything after it reads that set, so a later rewrite would make the lock and the specifier check describe a call that never runs.

| Event | Runs for |
|---|---|
| `tool.start.chat-run` | a tool the agent chose during a chat turn |
| `tool.start.chat-restart` | the user re-running an existing call |
| `tool.start.command` | the user running the tool through its `/tool:<name>` command |
| `tool.start.agent-task` | a tool the agent chose inside a task |
| `tool.start.script` | a tool a Lua script called through `dass.tool.call` |

A `hook-type` of `tool.start.*` covers all five. Script-launched calls reach this point and the result point, but never the confirmation point.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `tool` | string | the **bare** tool name, e.g. `fs-edit`; the origin-qualified name is in `hook_name` and in what `hook-name` matched |
| `source` | string | `chat-run`, `chat-restart`, `command`, `agent-task`, `script` |
| `behaviour` | list or nil | declared behaviour flags in kebab case |
| `arguments` | object or nil | the effective argument set - what the tool would really run with |
| `chat_id` | number or nil | the chat the call belongs to |
| `agent_id` | string or nil | the agent whose message owns the call |

`arguments` is the set as it stands when the event fires. A rewrite made by an earlier hook of the same event is not reflected in it: that arrives in `previous`.

For an out-of-process engine these arrive as variables of the same names; with `json-input: true` the two structures are JSON text. `hook_type` and `hook_name` are separate variables in every engine.

## Verdict

| Field | Type | Effect |
|---|---|---|
| `decision` | string or nil | `disallow` refuses the call as unwanted, `error` refuses it as unworkable; `none`, `allow` and `ask` mean "no objection" |
| `reason` | string | the text the model reads instead of a result; required in practice for a refusal |
| `arguments` | object | replaces the argument set the tool runs with |
| `persists_arguments` | bool | with `arguments`, also replaces the model's persisted intent |

```lua
return { decision = "disallow", reason = "..." }
return { decision = "error", reason = "the path does not exist" }
return { arguments = { path = "other.txt" } }
return previous
```

## What the pipeline does with it

- `disallow` sets the call's status to **Rejected**: the user sees a turned-down call, the model reads `reason`.
- `error` sets it to **Error**: the call could not be carried out. Otherwise identical.
- `allow` and `ask` are the same thing here. The policy stage has not decided yet, so this point cannot raise a decision - only refuse one. The same words mean something else at [tool.confirm.md](tool.confirm.md).
- `arguments` without `persists_arguments` goes to the ephemeral override: the transcript keeps showing what the model asked for. With the flag it replaces the persisted arguments too.
- An out-of-process hook that exits with code **2** does not need a verdict: its error text becomes a refusal with status Rejected before the chain's value is read.
- The refusal text is written by the pipeline, so settlement never overwrites it with "Tool failed with no result".
- The chain replaces: each sequential hook returns the whole document. A hook with nothing to add returns `previous`.

## What bites

- A `reason`-less refusal falls back to a generic sentence. Always supply one.
- `arguments` that is not an object is ignored with a warning in the log.
- A refusal and an argument rewrite never combine: a refused call does not run at all, so its arguments are dropped.
- `decision = "deny"` is not a word. An unknown word fails deserialization and the hook silently becomes "no opinion".
- A call to a tool that does not exist matches `unknown.<name>` and arrives with the emitted name in `tool`.
- A Lua hook that calls a tool is itself a `script` source: if its `hook-type` also matches `tool.start.*`, it re-enters its own start hook.

## Neighbours

- The confirmation point: [tool.confirm.md](tool.confirm.md)
- The result point: [tool.result.md](tool.result.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
