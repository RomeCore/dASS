# chat.turn.stop.{complete,interrupted,cancelled}

Fires once, when the multi-agent turn is over, from the `finally` that every exit path passes through - a return because no agent was left, a follow-up barrier, a cancellation and a throw all end here.

| Event | Runs for |
|---|---|
| `chat.turn.stop.complete` | no agent is left and nothing is pending |
| `chat.turn.stop.interrupted` | a follow-up message is waiting, so the turn ends and the next one picks it up |
| `chat.turn.stop.cancelled` | the turn was cancelled before it finished |

A `hook-type` of `chat.turn.stop.*` covers all three.

Fire-and-forget: the turn is over.

The point has no target: `hook-target` cannot narrow it.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `reason` | string | `complete`, `interrupted` or `cancelled` - the same word as the event suffix |
| `agents` | list | `[{ agent_id, agent, stage_id }]` of the agents the turn selected, in order |

## Verdict

None. The turn is over.

## What the pipeline does with it

- Nothing further in this turn. With `interrupted` the caller starts the next turn, which begins with its own `chat.turn.begin`.
- The execution status icons are already reset by the same `finally`; the hook runs after that.

## What bites

- **One turn call, one stop.** A follow-up barrier ends this turn and starts another, so a batch of two answers produces two `turn.begin`/`turn.stop` pairs, not one.
- `cancelled` is the token, not a decision: pressing Stop cancels the turn and lands here.
- `agents` lists what the turn **selected**, so an agent that was selected and then failed is in the list anyway. It is not a list of successful runs - `chat.turn.agent.stop.*` carries the per-agent outcome.
- An exception still fires this event; the turn is over even when it ended badly.
- The hook runs detached. It may still be running when the next turn starts.

## Neighbours

- The start of the turn: [chat.turn.begin.md](chat.turn.begin.md)
- One agent's turn: [chat.turn.agent.stop.md](chat.turn.agent.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
