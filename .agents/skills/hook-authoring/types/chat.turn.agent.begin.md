# chat.turn.agent.begin

Fires inside the ordering service, when the turn has to pick the next agent - after the previous agent finished its message with no tool calls, and before the stages choose.

The hook may answer instead of the stages: name an agent and it runs, or refuse and the turn ends. This is the only chat point where a hook returns a *selection*.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `messages` | list | the messages of the current round, in history order |
| `messages[].message_id` | number or nil | database id |
| `messages[].role` | string | `user` or `assistant` |
| `messages[].sender_id` | string | the login of the user or the id of the agent |
| `messages[].sender` | string or nil | the display name |
| `messages[].content` | string or nil | the **model-facing** text, additional parts included |
| `executed_in_this_round` | list | `[{ agent_id, agent, stage_id }]` of the agents that already ran, in order |
| `previous_agent` | table or nil | the agent that ran last; nil when the round is starting |
| `available` | list | `[{ agent_id, agent, stage_id }]` of every agent in every **enabled** stage |

The round is the trailing run of consecutive user and assistant messages - the same window the execution stages are chosen against - so the document is bounded by the round, not by the history.

## Verdict

`decision`, `agent` and `reason`:

| Field | Effect |
|---|---|
| `agent` (id or name) | that agent runs next; the stages are skipped |
| `decision = "disallow"` | the turn ends as if no agent had been left |
| `decision = "error"` | the same, and it is logged as a failure rather than a decision |
| nothing / `allow` | the stages choose, exactly as without a hook |

The stage of a named agent is resolved the way a forced agent's is: the first **enabled** stage containing it, then the first stage containing it, then an empty id. An id or name that resolves to nothing is a warning and "no opinion" - a typo in a selector must not silence the chat.

## What the pipeline does with it

- The chosen agent runs with the returned stage, and `executed_in_this_round` grows as the turn continues.
- A refusal ends the turn: `turn.stop` fires with `complete` or `interrupted` like any other ending. At the very first cycle the user also sees the generic "agent selection failed" warning - refuse the turn at `chat.turn.begin` instead when the user has to be told why.
- Several sequential hooks chain their verdicts: each gets `previous` and its return replaces it whole.

## What bites

- **It does not fire every cycle.** A forced agent returns before this point, and an agent that continues its own tool batch does not come here either - that is [chat.turn.agent.continue.md](chat.turn.agent.continue.md).
- `available` lists only enabled stages. Naming an agent from a disabled stage still works, but gives it an empty stage id - the same treatment a forced agent gets.
- `content` is the model-facing text, not what the user sees: a slash command may have hidden it or replaced it with its bare token.
- A name is matched case-insensitively against every configured agent, and the first match wins. Agents are not required to have unique names.
- The point has no target: `hook-target` cannot narrow it.
- The document grows with the round, not with the history - but a round of huge pastes is a large document.

## Neighbours

- The start of the turn: [chat.turn.begin.md](chat.turn.begin.md)
- The other kind of "an agent runs": [chat.turn.agent.continue.md](chat.turn.agent.continue.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
