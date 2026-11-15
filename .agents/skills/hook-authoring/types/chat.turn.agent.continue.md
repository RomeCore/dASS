# chat.turn.agent.continue

Fires when an agent runs again **without** a fresh selection. There are two such moments, and the reason says which:

| `reason` | When |
|---|---|
| `pending-tool-calls` | the turn found a message with unanswered tool calls and continues its sender - the work was cut short by a restart or a new turn |
| `tool-cycle` | the agent produced tool calls, the batch ran, and it continues with the results |

Fire-and-forget: nothing a hook returns could change the continuation, and there is no selection to influence.

The target is `{agent id}.{agent name}`.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `agent_id` | string | the agent that continues |
| `agent` | string | its name |
| `stage_id` | string | the stage it runs in |
| `cycle` | number or nil | the zero-based cycle about to run; **nil** for a resume |
| `reason` | string | `pending-tool-calls` or `tool-cycle` |
| `pending_tool_calls` | list or nil | the names of the calls the agent has to finish; nil for a cycle |
| `last_message_id` | number or nil | the database id of the message being continued |

## Verdict

None. The agent continues either way.

## What the pipeline does with it

- Nothing: the run proceeds exactly as without a hook.
- `pending-tool-calls` fires once per turn at most, before the resumed agent's first message.
- `tool-cycle` fires once per cycle after the first: a run with three tool batches produces it twice.

## What bites

- `cycle` is nil on a resume: the earlier cycles belong to a previous turn and cannot be counted without walking the history.
- The two reasons are different moments of the same pipeline. `pending-tool-calls` means no selection happened, which is why `chat.turn.agent.begin` did not fire; `tool-cycle` means the selection happened earlier and this is the same agent again.
- A run that ends with no tool calls fires no continuation - it fires `chat.turn.agent.stop` instead.
- `pending_tool_calls` carries names, not arguments: the arguments live in the history.

## Neighbours

- The selection that started the run: [chat.turn.agent.begin.md](chat.turn.agent.begin.md)
- The end of the run: [chat.turn.agent.stop.md](chat.turn.agent.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
