# chat.turn.agent.stop.{success,error,cancelled}.{complete,interrupted}

Fires once, when one agent's turn is over - after its last message and after the tool batch of that message, before the turn moves on to the next agent.

| Event | Runs for |
|---|---|
| `chat.turn.agent.stop.success.*` | the agent's last message finished normally |
| `chat.turn.agent.stop.error.*` | the agent's last message carries an error |
| `chat.turn.agent.stop.cancelled.*` | the run was cancelled |

The last variant says why the agent stopped:

| Reason | Meaning |
|---|---|
| `complete` | the agent produced a message with no tool calls and nothing cut it short |
| `interrupted` | a follow-up message the user wrote while the agent ran took precedence; the agent's turn ends here instead of moving on |

A `hook-type` of `chat.turn.agent.stop.*` covers every combination; matching only the reason is not possible with one glob, so branch on `payload.reason` instead.

Status and reason are repeated in the payload because a hook that matches the whole family needs them in the document.

Fire-and-forget: the run is already over.

The target is `{agent id}.{agent name}`.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `agent_id` | string | the agent whose turn ended |
| `agent` | string | its name |
| `stage_id` | string | the stage it ran in |
| `status` | string | `success`, `error` or `cancelled` - the first variant |
| `reason` | string | `complete` or `interrupted` - the last variant |
| `cycles` | number | how many messages the agent produced in this run |
| `message_ids` | list or nil | the database ids it left behind; nil when none is saved |

## Verdict

None. The run is over.

## What the pipeline does with it

- The turn continues: the next agent is selected, or the turn ends if the follow-up queue took over.
- `interrupted` matches a `chat.turn.stop.interrupted` - the same follow-up ended both.

## What bites

- **This is not per message.** A run with three tool batches is one `agent.stop` and four `message.assistant.stop` events.
- `status` is the status of the agent's **last** message. A run that produced no message at all - the agent could not be resolved, the very first request failed - reports `error`, or `cancelled` when the turn was cancelled.
- `cycles` counts messages, not tool calls.
- `reason` is not in the same axis as `status`: a run can be `success` and `interrupted` at once, and that is the normal case for a follow-up cut-in.
- The hook runs detached. It may still be running when the next agent starts.

## Neighbours

- Every message of the run, one by one: [message.assistant.stop.md](message.assistant.stop.md)
- The end of the whole turn: [chat.turn.stop.md](chat.turn.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
