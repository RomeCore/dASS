# 11: Verification pass

Status: open
Type: task
Blocked by: 09, 10

## What to verify

The agreed behaviour end to end, on a running build, with a real model and at least one tool that takes a few seconds.

## Matrix

Every cell of the input matrix, in both clients:

| Run | Input | Expected |
|---|---|---|
| running | non-empty, Enter | entry queued, run continues, text cleared, tab count +1 |
| running | non-empty, Ctrl+Enter | same |
| running | empty, Enter | run cancelled, queue injected, new generation |
| running | empty, Ctrl+Enter | run cancelled, queue untouched |
| running | empty, Stop button | same as Ctrl+Enter |
| idle | non-empty, empty queue | ordinary send |
| idle | non-empty, non-empty queue | new message last, whole queue inserted, one answer |
| idle | empty, non-empty queue | queue injected, then an answer only if an entry asked |

## Barriers

- `Message` entry: injected after the tool batch of the current message, before the next message cycle.
- `Agent` entry: injected after the agent's last message and after its execution-finished hooks.
- `AgentSequence` entry: injected before the next agent is selected.
- An entry with a later level waits while an earlier one triggers the flush: the whole queue goes at the earliest gate.

## Commands and permissions

- A queued command runs once, in order, and the next message waits for it.
- An invalid command is refused at enqueue time with the text preserved; a command that becomes invalid before
  insertion is inserted with the error attached and no generation.
- `CanExecuteCommand = false` inserts `/tool:...` verbatim with no execution and no fingerprint.

## Persistence and failure

- Queue two entries, kill the app, reopen: both are pending, in order, with parts and levels.
- A hard stop leaves the queue untouched.
- An error mid-run leaves the queue untouched and does not trigger a drain.

## Cross-cutting

- AR-0027: two browsers, one participant typing, the other sending - the caret survives.
- Restarting generation (regenerate) with a non-empty queue: the answer comes first, the queue after.
- Stop pressed twice in a row: no duplicated message, no double answer.
- The tab strip never covers the scroll-to-bottom button, and the history does not shift when a tab appears.
