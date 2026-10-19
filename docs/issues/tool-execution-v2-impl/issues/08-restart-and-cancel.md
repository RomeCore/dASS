# P8 — Restart and cancel of a single tool

Status: resolved
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

Both actions are **inline** on the tool call (`ToolCallView`), not in the flyout: cancel on a
running/awaiting call, restart on a completed one. No auto-turn button.

## Acceptance

- Cancelling one tool leaves the run and its siblings alive.
- Restarting a completed tool updates the call and the next model request sees the new result.
- Unit test: a cancelled call keeps its partial output.

## Answer

The phase closed the two holes P4 left behind: nothing was dispatching `ChatRestart`, and nothing could
call `Cancel()`.

- **`IChatToolCallService` / `ChatToolCallService`** (chat-scoped, `Tools/Execution/`) is the user's entry
  point. `CanCancel` = not finished and a live source; `Cancel` cancels only that call.
  `CanRestart` = finished and the tool still exists in the chat's **available** tools (the same surface
  the tool commands use — a user action is not an agent decision, so no per-agent overrides, no policy,
  no confirmation); `RestartAsync(host, call, ct)` dispatches `ChatRestart` on the same call.
- **`ToolCallViewModel`** exposes `CanCancel`, `CanRestart`, `CancelCommand`, `RestartCommand`. Its
  subscription to the call is no longer dropped when the call reaches a terminal status: a restart
  re-runs a *finished* call on the same object, so a terminal status is not the end of the card.
- **`ToolCallView`** carries both actions inline (cancel `StopCircle`/red, restart `Refresh`/blue) with
  new locale keys `tool.call.action.cancel` / `tool.call.action.restart` in `iv` and `ru-RU`.
- **`ChatConfirmationSink`** drops the pending confirmation source when the wait is cancelled, so a card
  that is already closed does not leave a live TCS behind it.
- **`ToolInitializationService`** also resets the progress of the previous run (`SetProgress(null, 0, 1)`);
  the reset and the token relinking were already landed in `919882df`.

### Divergences from the ticket as written

1. **`CanCancel` is computed**, not a field: it is a pure function of `ToolCall.Status` and
   `ToolCall.CancellationTokenSource`, exactly like `InProgress` / `UserAsked`, and the view model
   re-raises it when either changes. `CanRestart` stays a cached flag, because the answer is not a
   property of the call — it needs the tool, so it is refreshed on status/source changes instead of on
   every binding read.
2. **The commands carry no `CanExecute`.** Visibility is the gate (`CanCancel` / `CanRestart` bind
   `IsVisible`) and the service guards internally, so there is no second copy of the same rule to keep
   in sync.
3. **Restart with edited arguments has no producer.** `call.Arguments` is the persisted channel and a
   restart writes the call before the run, but no surface produces the edit; recorded in
   `../not-implemented.md`.
4. **Restart is fire-and-forget from the UI** (`AsyncRelayCommand`, `CancellationToken.None`): it is not
   tied to a chat run token when it is started from a finished call, and it never starts a new turn.
5. **Task calls are out of reach.** `RestartAsync` takes a `ChatMessage` host because the chat context
   factory needs one; an `AgentToolCall` never gets a restart action on the task card.
