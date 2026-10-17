# P4 — Orchestrators and the dispatcher

Status: open
Type: task
Blocked by: 03

## Scope

Delete `IToolExecutionService` / `ToolExecutionService`; add the chat-side orchestrators and the
dispatcher (spec §5, ticket 03 of the spec effort).

## Dispatcher

`IToolInvocationDispatcher` is **Chat-scoped** and picks between the chat-side sources. The task path does
not go through it — `AgentTaskExecutor` calls `AgentTaskToolOrchestrator` directly.

```csharp
public interface IToolInvocationDispatcher
{
    Task RunAsync(ToolInvocationRequest request, CancellationToken ct);
}
```

`ToolInvocationRequest` is a plain class with init-only properties:

- `Source` (`ToolInvocationSource`)
- `Call` (`IToolCall`)
- `Descriptor` (`IToolDescriptor?` — `MissingToolDescriptor` is substituted when null)
- `Confirmation` (`IConfirmationSink`)
- `Host` (`IToolCallHost?`), `ArgumentStream` (`IToolArgumentStream?`),
  `EffectiveBehaviours` (`ToolPolicyMask`), `RunningInUi` (`bool`)

`StreamingMode` is chosen by the orchestrator from the source; it is not in the request.

## Orchestrators

`Tools/Execution/Orchestrators/`, all Chat-scoped except the task one:

| Orchestrator | Sequence |
|---|---|
| `ChatToolOrchestrator` | streaming (Cyclic) → validation → sync{ preview → policy → confirmation → execution } → settle |
| `ChatRestartOrchestrator` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `ToolCommandOrchestrator` | streaming (Once) → validation → sync{ preview → execution } → settle (hard floor honoured) |
| `ScriptToolOrchestrator` | streaming (no-op) → validation → sync{ preview → execution } → settle (no policy/confirmation/hard floor) |

Sequences are explicit imperative code; shared is only the stage services and the sync higher-order
wrapper. Settlement runs in `finally`.

## Wiring

- `ChatExecutionService` builds the chat confirmation sink, creates the domain call, and calls the
  dispatcher with `ChatRun` instead of `IToolExecutionService.ExecuteAsync`.
- The streaming adapter (`IToolArgumentStream` over `PartialFunctionToolCall`) is created at the entry
  point; drain lives in `IToolStreamingService`.
- `ToolInfo.ContextFactory` is stamped by `ToolsetCollector` on the per-chat clone before freezing.
- `Script` entries (`LuaApiTools`, later `LuaApiAgents`) call the dispatcher with `Script`.

## Acceptance

- `IToolExecutionService` and `ToolExecutionService` do not exist.
- A chat turn runs tools through `ChatToolOrchestrator`; the run's cancellation token reaches every call.
- Manual smoke: one approving tool, one auto-approved tool.
