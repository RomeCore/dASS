# 06: Stop, hard stop, immediate level and the idle wait

Status: resolved
Type: task
Blocked by: 05

## What to build

The three ways a run ends early, and the one wait they all share.

See [`spec.md`](../spec.md) sections *Flows* (stop and flush, hard stop, immediate level).

## Contracts

```csharp
public interface IChatExecutionStatusService
{
    // Completes when no WithExecution scope is alive.
    Task WaitUntilIdleAsync(CancellationToken ct = default);
}

public interface IChatOperationService
{
    Task StopGenerationAsync(CancellationToken ct = default);   // cancel the root token, nothing else
    Task StopAndFlushAsync(CancellationToken ct = default);     // cancel, wait for idle, drain, generate
}
```

## Rules

- Idleness is `Status == Idle`, driven by `WithExecution`; never the token service. `WithToken(Operation)` removes and
  cancels the previous level entry, so the dying run's `ReleaseLevel` finds nothing and the token service cannot report
  when it finished unwinding (trace finding F5).
- `WaitUntilIdleAsync` subscribes to `PropertyChanged`, returns immediately when already idle, and must not leak the
  subscription on cancellation.
- `StopAndFlushAsync`: `root.Cancel()` -> `await WaitUntilIdleAsync(ct)` -> take `Operation` -> turn flush-first with
  `generate: true`. The wait precedes the scope, so the new operation does not compete with the dying one.
- `StopGenerationAsync` is what the Stop button and Ctrl+Enter-on-empty use: cancel only, no flush, no turn.
- `Immediate` level: `QueueUserInputAsync` stores the entry first, then calls `StopAndFlushAsync`. A tool that ignores
  its token holds the injection for a moment; the status bar shows the run stopping, which is honest.
- Nothing here touches the queue on a hard stop.

## Acceptance

- Enter on an empty input during a run: the visible message is `Cancelled`, the queue is inserted, one new answer
  starts.
- Ctrl+Enter on an empty input during a run: the run stops and the queue is untouched.
- The Stop button equals Ctrl+Enter on an empty input.
- An entry queued with `Immediate` during a run stops it and injects the entry without waiting for a barrier.
- `StopAndFlushAsync` pressed twice: the second run cancels the first, no entry is inserted twice, no double answer.

## Not in scope

The key handling itself (ticket 08).

## Answer

`WaitUntilIdleAsync` lives on `IChatExecutionStatusService` and is driven by `WithExecution`, not by the token service: taking a level erases the entry it just cancelled, so tokens cannot report the end of the run they killed. `StopGenerationAsync`, `StopAndFlushAsync` and the `Immediate` enqueue path landed; the wait happens before the new operation is taken, so the new turn does not compete with the dying one. Known risk, left open and recorded in the verification answer: the wait has no timeout.
