# chat.turn.begin

Fires once, at the start of a multi-agent turn: after the user message is in the history and before any agent is selected, any message is created or the prompt is assembled.

It is a circuit breaker for the whole turn, not a prompt filter - the payload is deliberately thin. A hook that wants to decide by what was asked belongs on [chat.turn.agent.begin.md](chat.turn.agent.begin.md), which is asked the same turn one step later and carries the whole round.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `last_message` | table or nil | the last message of the history; nil when the chat is empty |
| `last_message.message_id` | number or nil | its database id, nil when unsaved |
| `last_message.role` | string | `user`, `assistant` or `other` |
| `last_message.sender_id` | string | the login of the user or the id of the agent |
| `last_message.sender` | string or nil | the display name |
| `last_message.content` | string or nil | the model-facing text |

For an out-of-process engine these arrive as variables of the same names; `hook_type` and `hook_target` are separate variables in every engine.

## Verdict

`decision` and `reason`:

| Word | Effect |
|---|---|
| absent / `allow` | the turn starts normally |
| `disallow` | the turn does not run at all |
| `error` | the same, and the user is told a hook failed rather than that it refused |

`reason` is shown to the user as the toast text. Without one, a localized default is shown.

## What the pipeline does with it

- A refusal returns from the turn immediately: no agent is selected, no message is created, no request is sent. The user's message stays in the history, unanswered.
- The follow-up queue is untouched. The next turn - the user's next send, a flush, a stop-and-flush - drains it as usual.
- `turn.stop` does **not** fire for a turn that never began.

## What bites

- The payload is thin on purpose. "Refuse turns about X" cannot be answered here: use `chat.turn.agent.begin`.
- A refusal is announced with a toast. Blocking every turn while the user is away from the screen is not a silent operation.
- `chat.turn.begin` fires once per *turn call*, and a follow-up barrier makes the caller start another turn - see [chat.turn.stop.md](chat.turn.stop.md).
- The point has no target: `hook-target` cannot narrow it.

## Neighbours

- The choice of the agent, one step later: [chat.turn.agent.begin.md](chat.turn.agent.begin.md)
- The end of the turn: [chat.turn.stop.md](chat.turn.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
