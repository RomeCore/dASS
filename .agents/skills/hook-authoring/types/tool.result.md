# tool.result.{source}.{status}

Fires **after** the call is settled: the status is final, the text the model will read exists, the synchronization lock is released. The point is for auditing and for fixing up what the model reads - it cannot change what happened.

| Event | Runs for |
|---|---|
| `tool.result.chat-run.success` | a successful call the agent chose |
| `tool.result.*.error` | any failure, whatever launched it |
| `tool.result.{chat-run,agent-task}.rejected` | refusals, not user cancellations |
| `tool.result.*` | every settled call |

`source` is `chat-run`, `chat-restart`, `command` or `agent-task`; `status` is `success`, `error`, `cancelled` or `rejected`. Putting the outcome in the name is what lets a hook target a single case without branching in its body.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `tool` | string | the name `hook-name` matched: `{origin}.{name}` |
| `source` | string | who launched the call |
| `status` | string | `success`, `error`, `cancelled`, `rejected` |
| `arguments` | object or nil | the argument set the call ran with |
| `behaviour` | list or nil | behaviour flags in kebab case |
| `result` | string or nil | the text the model will read |
| `markdown` | bool | whether that text is Markdown |
| `error` | string or nil | the exception message, filled for the `error` outcome only |
| `error_type` | string or nil | the exception type name, likewise |
| `chat_id`, `agent_id` | number / string or nil | the owning chat and agent |

## Verdict

| Field | Type | Effect |
|---|---|---|
| `result` | string | replaces the model-facing text; empty or whitespace is ignored |
| `markdown` | bool | replaces the call's Markdown flag |

```lua
return { result = payload.result:gsub("/home/[^/]+/", "~/") }
return { result = "The file does not exist; use fs-glob to find it.", markdown = false }
return previous
```

## What the pipeline does with it

- The status, the structured result and the attachments are already final and stay untouched.
- Settlement has already filled a default text when the tool produced none, so `payload.result` is never empty for a hook.
- Hooks run in order, after the lock, before the cancellation source is released. A hook that reads the file the call just wrote takes no lock.
- A blocking message has nothing to stop here: the call is over, and the message is only logged.

## What bites

- `result = ""` does not blank the text; the empty value is ignored on purpose.
- A result hook cannot turn a failure into a success. If the outcome matters, the call already had it.
- A `Markdown` value without `result` is applied to the existing text - useful for a hook that only reformats.
- Script-launched calls are never hooked, so a result hook never sees the tools a script called.

## Neighbours

- The start point: [tool.start.md](tool.start.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
