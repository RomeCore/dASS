# message.assistant.stop.{success,error,cancelled}

Fires once, when an assistant message reaches a terminal status - after the tool batch of that message has been awaited, and before the next cycle starts.

| Event | Runs for |
|---|---|
| `message.assistant.stop.success` | the generation finished |
| `message.assistant.stop.error` | the generation failed; the message carries an error |
| `message.assistant.stop.cancelled` | the turn was cancelled |

A `hook-type` of `message.assistant.stop.*` covers all three.

The status is repeated in the payload because a hook that matches the whole family needs it in the document. A message that never reached a terminal status - the prompt could not be built, the request could not be sent - is reported as `error`, or as `cancelled` when the turn was cancelled.

Fire-and-forget: the message is committed, so nothing a hook returns could change it.

The target is `{agent id}.{agent name}`.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `message_id` | number or nil | the database id of the message |
| `index` | number or nil | the 1-based index of the message in the history |
| `agent_id` | string | the agent that answered |
| `agent` | string | the name of that agent |
| `stage_id` | string | the execution stage the agent ran in |
| `cycle` | number | the zero-based cycle of this agent that produced the message |
| `status` | string | `success`, `error` or `cancelled` - the same word as the event suffix |
| `content` | string | the text the message ended with, possibly empty |
| `tool_calls` | list or nil | `[{ name, status }]`, nil when the message carries none |
| `usage` | table or nil | `model`, `input_tokens`, `output_tokens`, `cache_create_tokens`, `input_cache_hit_tokens`, `input_cache_miss_tokens`, `time_to_first_token`, `generation_time`; nil when the model reported nothing |
| `error` | string or nil | the rendered error text, though it is localized on read rather than paired with a key |
| `is_user_like` | bool or nil | whether the assistant message identifies itself as a user |

For an out-of-process engine these arrive as variables of the same names; `hook_type` and `hook_target` are separate variables in every engine.

## Verdict

None. The message is already committed and the point is an announcement.

## What the pipeline does with it

- Nothing: the agent's cycle continues with the next message, or ends.
- A run with tool calls produces one `stop` per message, so three tool batches end with three `stop` events - all before the agent-level turn ends.
- The hook runs detached. It may still be running when the next cycle starts, and it sees the message as it was at that moment.

## What bites

- `content` is the whole message, not a delta. The block-by-block streaming event does not exist yet.
- `usage` is nil when the provider reported no metadata, and its token fields are nil when the provider has no prompt cache.
- `error` is the rendered text; a script cannot re-localize it, and a log written from a hook keeps whatever language was active.
- `tool_calls` carry names and terminal statuses, not arguments - the arguments live in the history.
- A message left `Pending` by an early failure is reported as `error`. Do not treat the status as proof that the model was reached.

## Neighbours

- The start of the same message: [message.assistant.start.md](message.assistant.start.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
