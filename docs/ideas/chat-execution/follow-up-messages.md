# Follow-up messages accumulated while an agent run is in progress

Status: open
Type: task
Blocked by:

## Requested

- A queue of follow-up messages that can be filled while the chat is busy, persisted as part of the chat's
  additional data.
- A follow-up level chosen per message in a combo box beside the sender and visibility selectors of the input:
  immediately (cancel the running generation, insert, run commands), after the tool batch of each message, after
  one agent's cycle, after the whole agent sequence.
- The level sets how often insertion is checked; a passing check means the messages may be inserted and
  generation may continue.
- A passing check returns execution to the multi-agent entry point, because a follow-up must be equal to an
  ordinary message.
- Slash commands are supported from the start: they run one at a time, one message is taken off the queue per
  command, and the next message is tried once the command completes.

## Proposed design

- **One additional-data item per queued message, not one item holding the list.** Chat additional data is
  persisted item by item, each with its own order and change tracking, so appending, removing and editing a
  queued message persist without a new document shape; a nested list inside a single item is only tracked at the
  top level. An item carries what the input state already carries (text, parts, sender, visibility) plus the
  chosen level and the command intent.
- **Levels are thresholds on barriers, not polling frequencies.** The barriers are: a tool batch closed, an
  agent cycle closed, an agent sequence closed. A message is injected at the first barrier whose rank reaches its
  own level, and a barrier takes every eligible message at once - one injection, one answer. Checking on a
  frequency would otherwise drip level-two messages at the last barrier one at a time, against the
  accumulate-and-inject-at-once intent.
- **"Return to the entry point" is an exit, not a recursive call.** The multi-agent entry already holds the
  sequence scope; calling it again from inside a run would take that scope afresh, and taking a scope cancels
  every scope at or below it - including the caller's own token, so the run dies instead of continuing. The
  current agent execution must end early at the barrier and let the existing multi-agent loop re-route, which
  yields the requested "follow-up equals ordinary message" behaviour without re-entrancy.
- **A command cannot run under the scopes the run is holding.** The command path takes the command scope, which
  cancels the agent and message scopes; running it at an in-run barrier would kill the turn it was meant to join.
  The follow-up path must reach command execution with the caller's token instead of taking a scope, and must
  pass through the existing send pipeline rather than re-implementing insertion - otherwise "equal to an ordinary
  message" is a promise rather than a property.
- **"Immediately" is an action, not a level.** It waits for no barrier: it cancels and must then wait for the
  chat to become idle before inserting, or two executions interleave and both write the history. The execution
  token service does not expose that idle wait today. Recommended: keep the three barrier levels as one ordered
  scale and expose interrupt-and-send as a separate input action.
- **The batch takes one generation decision**: any intent that asks for a response generates once for the whole
  batch, and a command lowering its own intent does not lower the batch.
- **Pending messages are inserted before a freshly typed one** when the queue outlived a run (stop, failure), so
  the transcript order matches the order in which the user asked.
- **A re-routed follow-up must not inherit the run's "already produced cycles" state**: agent selection stays
  silent about its failure once any agent has run, so a follow-up that finds no agent would end with the message
  unanswered and no report.
- Accepted as proposed: the per-message level selector, the three barrier points, one-command-at-a-time
  execution, and persistence in the chat's additional data.

## Not in scope

- Merging accumulated messages into a single message: it would lose per-message sender and visibility, which the
  multi-user chat relies on.
- Auto-sending a restored queue when a chat is opened: a pending message is shown and acted on by the user.
- Changing stop semantics: stop stays "cancel everything", and the queue survives it.
- Queueing messages for the agent tasks and sub-agents that run outside the chat's turn pipeline.

## Comments

- Open: whether "immediately" ships in the same change or after the barrier levels.
- Open: whether a command-bearing message is allowed at every barrier, or only at the barrier where no narrower
  scope is held, if the token seam is not part of this change.
