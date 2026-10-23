# Pipeline hook events

Status: open
Type: task
Blocked by:

## Requested

- A set of scripting hooks covering the chat execution pipeline, the tool execution pipeline and the chat lifecycle.
- An explicit equivalent of the "stop" hook of another agent runtime: something that fires when the agent has finished responding.
- Each hook described in detail, together with the places it is useful.
- Hooks are not the existing chat execution event handlers. Those stay event handlers; this is a separate mechanism with a decision in its return.

## Proposed design

The event set, its placement and whether it can hold the pipeline:

| Event | Placement | Blocking |
|---|---|---|
| `user-prompt-submit` | The send path, after a slash command is resolved, before the message is persisted or the command runs | yes |
| `agent-select` | The agent ordering decision, before the stage loop yields the next agent | yes |
| `prompt-build` | Final request assembly, after effective messages and the header are resolved | yes, restricted |
| `assistant-message` | After a response is materialised, before its status is finalised | yes |
| `agent-stop` | An agent produced a message with no tool calls | yes |
| `turn-stop` | The round ended and no agent was selected | observe only |
| `turn-interrupted` | The turn was cancelled | observe only |
| `tool-validate` | The validation stage, before synchronization, preview and policy | yes |
| `pre-tool-use` | After validation, before the synchronization lock is taken | yes |
| `tool-policy` | After the policy decision is computed, before confirmation | yes |
| `tool-confirm` | Only when the decision asks the user and before the confirmation surface is used | yes |
| `post-tool-use` | After execution and after the synchronization lock is released, before settlement | yes |
| `tool-error` | In settlement, when a thrown exception is classified | yes |
| `subagent-start` / `subagent-stop` | Around a sub-agent task, including the agent-call, command and task entry points | yes |
| `chat-open` / `chat-close` | When a chat scope is opened or disposed | observe only |
| `chat-create` / `chat-delete` | When a chat is created or deleted | observe only |
| `pre-compact` / `post-compact` | Around summarization and forced compaction | `pre` yes, `post` observe |
| `notification` | Toast and status surface | observe only |

The corrections, which matter more than the catalogue:

- **Stop is two events, not one.** A chat runs a chain of agents; an agent finishing and a round finishing are different moments. `agent-stop` may block and continue the same agent; `turn-stop` may not block, because no agent remains to continue.
- **A blocked stop continues the same agent.** The reason is delivered to the model as a hidden content part on that agent's own message, not as a synthetic user turn and not as a retry of the user's prompt.
- **A block is bounded and self-aware.** A per-turn block counter caps it, and the hook sees that it has already blocked once, so a guard cannot loop forever on the user's budget.
- **Cancellation is not a stop.** A cancelled turn fires an observe-only interruption event; holding a cancelled turn open would contradict the cancellation the user just requested.
- **A hook decision combines with policy, it does not replace it.** A hook may lower an ask to allow or to deny; it may not lift a hard deny unless the hook declares escalation explicitly.
- **A denial must write a model-facing result.** Settlement fabricates "returned no result" for an empty result, which would tell the model the opposite of what happened.
- **Argument rewriting uses the ephemeral override channel**, never the persisted model intent, so the transcript still shows what the model asked for.
- **The prompt hook cannot touch the anchored prefix.** In the anchored and static modes the prefix is kept byte-stable on purpose; the hook may filter the toolset and append to the request tail, and receives the boundary index so it can see the difference.
- **Post-tool hooks run outside the synchronization window.** A hook that reads a file must not hold the file lock of the call it is reacting to.
- **`post-tool-use` and `tool-error` are separate.** Reacting to a successful result and turning an exception into an actionable message are different jobs, and a single event would force every hook to branch.
- The four events that carry most of the value are `user-prompt-submit`, `pre-tool-use`, `post-tool-use` and `agent-stop`. They are the first implementation target; the rest can follow without changing the contract.

## Not in scope

- Where hook definitions live and how they are registered; that is a separate idea.
- Typed hooks written as native services rather than scripts.
- A settings interface for individual hooks.
- Hooks that call the model on the critical path.

## Comments

- The tool pipeline is shared by five entry points (chat run, chat restart, command, agent task, script), so the tool events sit in the stages rather than in the orchestrators; otherwise each entry point would need its own wiring.
- The validation stage is currently a stub that forces a tolerant parse, which makes it the natural home of the argument-rewriting event.
