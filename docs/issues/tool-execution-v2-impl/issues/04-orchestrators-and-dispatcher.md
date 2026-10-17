# P4 — Orchestrators and the dispatcher

Status: resolved
Type: task
Blocked by: 03 (complete; P3.5a/P3.5b also complete)

## Scope

Delete `IChatToolExecutionService` / `ChatToolExecutionService`; add the chat-side orchestrators, the task
orchestrator and the dispatcher (spec §5, ticket 03 of the spec effort). The orchestrator classes are
written in this phase; the task **wiring** stays in P5.

## Decisions settled with the user (2026-10-18)

1. **The dispatcher builds the context.** It substitutes `MissingToolDescriptor`, calls the request's
   `ContextFactory` and passes the ready `IToolExecutionContext` to the chosen orchestrator. Orchestrators
   never see `ToolInvocationRequest`.
2. **The dispatcher is Application-scoped and stateless.** It reads only request fields; chat-ness lives
   in the context factory, the confirmation sink and `EffectiveBehaviours`, all supplied by the entry
   point. This makes `AgentTask` a first-class source instead of an exception.
3. **`ToolInvocationRequest.ContextFactory` is required.** The entry point resolves it:
   `toolInfo?.ContextFactory ?? chatFactory` for the chat, a `ScriptToolContextFactory` for a script, a
   `TaskToolContextFactory` for a task (P5). The descriptor's stamped factory is honoured by the chat
   entry, never by the dispatcher.
4. **Orchestrators are Application-scoped.** They hold no chat state; stage services are Application
   singletons too.
5. **No factory/sink registration in DI.** `ChatToolContextFactory` and `ChatConfirmationSink` are
   constructed at their entry points (`new`), as before. Only the dispatcher and the orchestrators are
   services.
6. **Five orchestrators, no dedup.** `ChatRestartOrchestrator` and `ToolCommandOrchestrator` have
   identical sequences and stay two classes on purpose. `AgentTaskToolOrchestrator` is written in P4 but
   is wired to `AgentTaskExecutor` in P5 only.
7. **Settlement owns the terminal state.** `IToolSettlementService.Finalize(ctx, Exception? exception)` —
   the parameter has no default; every call site passes `null` or the exception. Orchestrators have no
   `finally`: they call `Finalize(ctx, null)` at the end of the `try` and `Finalize(ctx, ex)` in `catch`.
   The settlement classifies cancellation vs error and fills the default content.
8. **Validation throws.** `IToolValidationService.Validate` returns `void` and throws
   `ToolArgumentsInvalidException` (a `ToolInvocationException`); the message is model-facing and the
   settlement writes it verbatim. One error channel for the whole pipeline.
9. **No hard floor for user actions.** `ChatRestart` and `Command` skip policy, confirmation **and** the
   `AlwaysDisallow` check — the user's action is trusted (spec §13 updated).
10. **`Script` skips the synchronization stage.** Keys are not reentrant; an honest TODO in the
    orchestrator and an entry in `not-implemented.md`. Sequence: validation → preview → execution →
    settle.
11. **`Script` gets its own context.** `ChatScriptToolExecutionContext` + `ScriptToolContextFactory`
    implement the interface; no host message, no consent. `ChatToolContextFactory` is untouched.
12. **`ConstantConfirmationSink`** (`Sinks/`) with static `Approve` / `Disallow` replaces the private
    `RejectingConfirmationSink` in `ChatAgentTool`. Script/Restart/Command pass `Approve`.
13. A nested `dass.tool.call` uses `LuaCallingContext.CancellationToken` (AsyncLua's per-call token).

## Dispatcher

```csharp
public interface IToolInvocationDispatcher
{
    Task RunAsync(ToolInvocationRequest request, CancellationToken ct);
}
```

`ToolInvocationRequest` is a plain class with init-only properties: `Source` (`ToolInvocationSource`),
`Call` (`IToolCall`), `Descriptor` (`IToolDescriptor?`), `Confirmation` (`IConfirmationSink`),
`ContextFactory` (`IToolExecutionContextFactory`), `Host` (`IToolCallHost?`), `ArgumentStream`
(`IToolArgumentStream?`), `EffectiveBehaviours` (`ToolPolicyMask`), `RunningInUi` (`bool`).

`StreamingMode` is chosen by the orchestrator from the source; it is not in the request. An unknown
`Source` value throws `ArgumentOutOfRangeException`; no source is rejected "by design".

## Orchestrators

`Tools/Execution/Orchestrators/`, all Application-scoped:

| Orchestrator | Sequence |
|---|---|
| `ChatToolOrchestrator` | streaming (Cyclic) → validation → sync{ preview → policy → confirmation → execution } → settle |
| `ChatRestartOrchestrator` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `ToolCommandOrchestrator` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `ScriptToolOrchestrator` | validation → preview → execution → settle (no streaming, no sync, no policy/confirmation) |
| `AgentTaskToolOrchestrator` | validation → sync{ preview → policy → confirmation → execution } → settle (no streaming) |

Sequences are explicit imperative code; shared is only the stage services and the sync higher-order
wrapper.

## Wiring

- `ChatExecutionService` builds the chat confirmation sink and the chat context factory (fields), creates
  the domain call, computes `EffectiveBehaviours` from the agent it already has, and calls the dispatcher
  with `ChatRun`. `CanBeCompacted`/`Title` stay at the entry point.
- The streaming adapter (`IToolArgumentStream` over `PartialFunctionToolCall`) is created at the entry
  point; drain lives in `IToolStreamingService`.
- `ToolInfo.ContextFactory` is stamped by `ToolsetCollector` on the per-chat clone before freezing
  (already landed in P3).
- `LuaApiTools.Call` builds a `ScriptToolCall`, uses `ScriptToolContextFactory`, `ConstantConfirmationSink.
  Approve`, `Host = null`, and calls the dispatcher with `Script`. Unknown tool → `LuaRuntimeException`;
  a tool-level failure → `success = false` + content (no throw). The manual is corrected accordingly.

## Acceptance

- `IToolExecutionService` and `ToolExecutionService` do not exist. — **done**
- A chat turn runs tools through `ChatToolOrchestrator`; the run's cancellation token reaches every call.
- Manual smoke: one approving tool, one auto-approved tool.

## Answer

All five orchestrators, the dispatcher and the full wiring landed.

What changed:

- `Tools/Execution/Dispatch/` — `IToolInvocationDispatcher` + `ToolInvocationDispatcher` (Application-scoped,
  stateless). It substitutes `MissingToolDescriptor`, builds the context through
  `request.ContextFactory` and switches on `Source`. An unknown enum value throws
  `ArgumentOutOfRangeException`; there is no source rejected by design.
- `Tools/Execution/Orchestrators/` — `ChatToolOrchestrator`, `ChatRestartOrchestrator`,
  `ToolCommandOrchestrator`, `ScriptToolOrchestrator`, `AgentTaskToolOrchestrator`, all
  Application-scoped, no shared inner helper, no `finally` (settle at the end of the `try` or in `catch`).
  `AgentTaskToolOrchestrator` is written but not wired — P5 does that.
- `IToolSettlementService.Finalize(ctx, Exception? exception)` — no default. It classifies cancellation
  vs error, writes a `ToolInvocationException` message verbatim, prefixes an unexpected exception and
  fills the default content.
- `IToolValidationService.Validate` returns `void` and throws `ToolArgumentsInvalidException` (a new
  `ToolInvocationException`).
- `ToolInvocationRequest` gained the required `ContextFactory`.
- Script world: `ChatScriptToolExecutionContext` + `ScriptToolContextFactory` (no message, no consent),
  `ScriptToolCall` (`Tools/Execution/Calls/`), `ConstantConfirmationSink`
  (`Sinks/`, static `Approve`/`Disallow`; `ChatAgentTool`'s private rejecting sink removed).
- `LuaApiTools.Call` now dispatches with `Script`, `RunningInUi = false` (a nested call has no surface to
  render a form on; UI-requiring tools must take their non-interactive path instead of hanging), uses
  `LuaCallingContext.CancellationToken` and maps the call into the documented result table (`success` is
  `false` on a tool-level failure; only an unknown tool throws). The manual was corrected.
- `ChatExecutionService` builds the chat sink and the chat context factory, computes
  `EffectiveBehaviours` from the agent it already has, sets `CanBeCompacted` at the entry point and calls
  the dispatcher.
- Deleted: `IChatToolExecutionService` / `ChatToolExecutionService`. The three direct
  `ChatToolExecutionContext` casts in the memory modules are pattern matches now (a script call has no
  message).

Diverged from the earlier ticket text (and recorded in `plan.md` deviations 12–17): the dispatcher is
Application-scoped and takes the factory from the request; the request has no default settle parameter;
validation throws; `Script` skips synchronization (TODO + `not-implemented.md`); user actions do not check
the hard floor; the script world has its own context and factory.

Gates: core build 0 errors / 0 warnings; Desktop build 0 errors (pre-existing NuGet advisories only);
`LLMDesktopAssistant.Tests` 1233 total, 1232 passed, 1 skipped, 0 failed; `Desktop.Tests` 44/44.

New tests: `ToolOrchestratorTests` (sequence per source, denial stopping execution, exception handed to
settlement), `ToolInvocationDispatcherTests` (missing-descriptor substitution, descriptor pass-through,
source routing, unknown source), `ToolSettlementAndValidationTests` (cancellation, unexpected failure,
model-facing message).

Follow-ups: P5 wires `AgentTaskToolOrchestrator` into `AgentTaskExecutor` and deletes the duplicated block +
`ChatAgentTool`; script-source synchronization stays deferred.
