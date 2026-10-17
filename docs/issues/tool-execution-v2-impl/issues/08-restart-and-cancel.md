# P8 — Restart and cancel of a single tool

Status: open
Type: task
Blocked by: 06

## Scope

Per-invocation cancellation and the in-place restart (spec §10, ticket 10 of the spec effort).

## Cancellation

- `IToolCall.CancellationTokenSource` is a settable, reactive, runtime-only property (P1). Chat's
  `Domain.ToolCall` raises `PropertyChanged`; nothing serializes it.
- The orchestrator links it with the run token at invocation: the call's source cancels only that call,
  the run token cancels every call.
- A call awaiting approval is cancelled by resolving its consent as cancelled
  (`Status = Cancelled`, no reason).
- Output already produced stays on the call; a "cancelled" note is appended.

## Restart

- `ChatRestartOrchestrator` runs the whole sequence again on the **same** `IToolCall`: reset status,
  result, and status icon/title before the run; keep the descriptor.
- Optional edited arguments are written to `call.Arguments` (persisted) before the run.
- Resuming from a chosen stage is rejected.
- The model-facing tool message is built from `call.ResultContent` by `AgentPromptComposer` at the next
  request, so the in-place mutation is enough: **no automatic new turn**.
- Restart is enabled only for completed calls; cancel only while a call is running or awaiting approval.
- Restarting a `MissingToolDescriptor` call is disabled (nothing to re-run).

## UI

Both actions are **inline** on the tool call (`ToolCallView`), not in the flyout:
cancel on a running/awaiting call, restart on a completed one. No auto-turn button.

## Acceptance

- Cancelling one tool leaves the run and its siblings alive.
- Restarting a completed tool updates the call and the next model request sees the new result.
- Unit test: a cancelled call keeps its partial output.
