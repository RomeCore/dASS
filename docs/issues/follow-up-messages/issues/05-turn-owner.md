# 05: The turn in `ChatOperationService`

Status: resolved
Type: task
Blocked by: 01, 02, 03, 04

## What to build

The owner of the turn. `IChatOperationService` gains the drain loop, the queue-or-insert decision and the single
generation trigger; it becomes the only place that calls `GenerateResponseAsync` for a chat turn.

See [`spec.md`](../spec.md) sections *Ownership*, *The turn*, *Flows*.

## Contracts

```csharp
public interface IChatOperationService
{
    Task SendUserInputAsync(UserInput input, bool generate, CancellationToken ct = default);   // turn
    Task QueueUserInputAsync(UserInput input, bool generate, CancellationToken ct = default);  // no scope
    Task FlushFollowUpsAsync(bool generate, CancellationToken ct = default);                   // turn, flush-first
    // the rest of ticket 07
}
```

Private: `RunTurnAsync(bool flushFirst, bool generate, CancellationToken)` and `FlushAsync(CancellationToken)`,
exactly as written in the spec.

## Rules

- A turn starts by taking `ChatExecutionLevel.Operation`; the scope and the turn share one lifetime.
- `SendUserInputAsync` with an empty queue: append, run the command, then run the turn with the resulting intent.
- `SendUserInputAsync` with a non-empty queue: enqueue at the **tail** (level from `queue.DefaultLevel`, the generate
  intent, `CanExecuteCommand`), then run the turn flush-first with `generate: false` - the batch intent comes from the
  entries.
- `QueueUserInputAsync` takes **no** execution scope (stated in the XML doc as deliberate), runs the pre-flight,
  enqueues, clears the draft only after the entry exists, and delegates `Immediate` to ticket 06.
- `FlushAsync` per entry: `TryBeginNext` -> `AppendUserInputAsync` -> `Complete` (the entry is eaten here, before
  the command) -> `RunInsertedCommandAsync`, ORing `entry.GenerateIntent` with the command's own intent. `Abandon`
  plus rethrow only when the append failed, because then nothing reached the history.
- `RunTurnAsync` starts each iteration with `token.ThrowIfCancellationRequested()`: a cancelled turn never advances the
  queue. It ends when the queue is empty and the intent is spent.
- `FlushFollowUpsAsync` is the pass-through used by the "flush the queue" input action: `Operation` plus a turn with
  `generate: false`, because the entries' own intents decide.

## Acceptance

- Sending while idle with an empty queue behaves exactly as today.
- Sending while idle with three queued entries inserts them first, in order, then the new message, and generates once.
- A cancelled operation leaves the queue untouched.
- Two sends that race: the second cancels the first, and no entry is inserted twice.
- An exception from the append leaves the entry pending and stops the turn; an exception from a command leaves the
  message inserted, the entry gone, and stops the turn.
- While a long command runs, its message is already in the history and its queue entry is already gone.

## Not in scope

The key matrix (ticket 08), stop and immediate (ticket 06), regenerate and friends (ticket 07).

## Answer

`ChatOperationService` owns the turn: `RunTurnAsync` with both orders and `FlushAsync` as a per-entry loop. One refinement of the ticket text: the entry leaves the queue between its message and its command, not after the command. Appending is a synchronous write while a command can run for minutes, so the queue must never show an already-inserted message, and the removal cannot race a long command. `FlushOutcome.Inserted` stops the loop when another turn owns the head, which is what prevents a spin. The turn starts each iteration with a token check, so a cancelled turn never advances the queue.
