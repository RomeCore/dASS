# 05: Multi-level `IChatExecutionTokenService` (remove `Chat.GenerationCts`)

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Introduce a chat-scoped execution-token service that owns cancellation for every chat operation and replace the ad-hoc
CTS plumbing (`ChatOperationService._cts`, `Chat.GenerationCts`, the UI's `GenerationCts` reads).

`Operation` is the **widest** level: cancelling it (e.g. switching a branch, editing/deleting a message) must cancel
everything running in the chat — LLM generation, agent routing, commands. Levels nest inward
(`Operation > Command > AgentSequence > Agent > Message`) and may be **skipped** (a `Message` token without an active
`Operation` is valid).

Scope note: this ticket wires up **`Operation` only** (in `ChatOperationService`). Wiring `Command`/`Agent*`/`Message`
into `ChatExecutionService` is a separate, maintainer-owned change.

```csharp
public enum ChatExecutionLevel { None = 0, Operation, Command, AgentSequence, Agent, Message }

public interface IChatExecutionTokenService
{
    CancellationTokenSource? ExecutionCancellationToken { get; }  // alive while any level is; null when idle
    event Action? ExecutionCancellationTokenChanged;

    IDisposable WithToken(ChatExecutionLevel level, CancellationToken inputCt, out CancellationToken cancellationToken);
    bool TryCancel(ChatExecutionLevel level);
}
```

Rules:

- taking level `L` cancels/replaces the `L` token and cascades into all narrower levels;
- each level links the caller's `inputCt` **and** the nearest live wider level;
- `Dispose` releases `L` and cascades into narrower levels; a narrower level never cancels a wider one;
- `ExecutionCancellationToken` is created lazily with the first active level and released when the last one is released;
  its `.Cancel()` kills all levels.

## Acceptance criteria

- [ ] `IChatExecutionTokenService` + `ChatExecutionLevel` exist, registered as a chat-scoped service.
- [ ] `ChatOperationService` takes `Operation` for its mutating paths (send / edit / regenerate / resend / switch branch / edit / delete)
      and passes the resulting token into `ChatExecutionService.GenerateResponseAsync(ct)`.
- [ ] `Chat.GenerationCts` is deleted; no code reads or writes it.
- [ ] `UserInputViewModel` and the Blazor `GenerationReadinessService` use `ExecutionCancellationToken != null` for "is executing"
      and `ExecutionCancellationToken.Cancel()` / the changed event for cancel/refresh.
- [ ] Unit tests cover: level cancels/replaces deeper levels; skipping a level works; `Dispose` cascades inward;
      the shared token is created/released lazily; `TryCancel` returns whether the level was active.
- [ ] The solution builds; sending, cancelling and branch switching work in the app.
