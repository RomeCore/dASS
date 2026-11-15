# message.assistant.start

Fires when the pending assistant message has just been created and written to the history - before the request is assembled and before a single token arrives. The message has status `Pending` and no content.

The flow is **parallel**: the dispatcher waits for the hooks, but nothing they return reaches the pipeline. A slow hook delays the request by up to its timeout (2 s by default, 30 s ceiling); a hook that must not be waited for declares `fire-and-forget` itself.

The event has no variants. The target is `{agent id}.{agent name}`.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `message_id` | number or nil | the database id of the pending message, nil when the history entry could not be found |
| `index` | number or nil | the 1-based index of the message in the history |
| `agent_id` | string | the agent that is about to answer |
| `agent` | string | the name of that agent |
| `stage_id` | string | the execution stage the agent runs in |
| `cycle` | number | the zero-based cycle of this agent: 0 for its first message, 1 after its first tool batch, and so on |
| `model` | string | the model that will answer |

For an out-of-process engine these arrive as variables of the same names; `hook_type` and `hook_target` are separate variables in every engine.

## Verdict

None. The return value is dropped - refusing here would leave a pending message with no answer, so the point is not a gate.

## What the pipeline does with it

- Nothing: the point announces the message and then assembles the prompt.
- The pending message becomes the **ambient message** of the whole generation: inside the window `dass.chat.current_message()` returns the same `chat_message` handle `get_messages()` would, and a tool or a script called from this turn sees it. The window closes when the message ends.

## What bites

- It fires before the prompt is built, so a hook that reads the history sees the pending message as the last one, with empty content.
- `cycle` above 0 means this same agent has already answered and called tools at least once in this turn.
- Parallel is still awaited: a hook that blocks for two seconds adds two seconds to every generation. Declare `fire-and-forget` when the hook must not be on that path.
- The message is already committed, so a hook that wants to affect it writes through `dass.chat.current_message()` or `dass.chat.get_messages()`, not through a return value.

## Neighbours

- The end of the same message: [message.assistant.stop.md](message.assistant.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
