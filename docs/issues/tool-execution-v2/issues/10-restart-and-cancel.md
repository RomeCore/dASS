# Restart and cancel of a single tool

Status: resolved
Type: grilling
Blocked by: 03, 08

## Question

The user wants to restart a tool — optionally with edited arguments — and to cancel an individual tool that is running or awaiting approval. This ticket settles what a restart actually re-runs (the whole sequence, or from a chosen point), how the restart sequence relates to the streaming phase (kept for tools that do not report status during preview), what happens to output already produced, and how cancelling one tool is expressed and distinguished from cancelling the whole agent run. This is feature goal six.

## Answer

A restart re-runs the **whole sequence** from the `ChatRestart` orchestrator (settled in ticket 03: `streaming (Once) → validation → sync{ preview → execution } → settle`). It re-runs the **same `IToolCall`**, whose state is reset before the run (status / result / produced output), optionally with edited arguments (a user edit → `call.Arguments`, persisted). Resuming from a chosen stage is rejected: it would require entering an arbitrary stage — a stage engine.

### Cancellation

Each invocation carries its **own `CancellationTokenSource`**, linked to the run's token. It lives on **`IToolCall`** (a runtime-only member, next to the confirmation TCS) so the UI can cancel a specific call.

- Cancelling **one tool** cancels only its token — the run and its sibling tools continue.
- Cancelling the **run** cancels the chat-execution token, i.e. every call.
- A tool **awaiting approval** is cancelled by resolving its consent as "cancelled" (`Status = Cancelled`).
- **Output already produced is preserved**: the partial result / stream stays on the `IToolCall`, with a "cancelled" note appended.

## Comments

<!-- appended conversation -->
