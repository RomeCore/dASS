# P7 — Cascade approval

Status: open
Type: task
Blocked by: 06

## Scope

When a decision's memorization scope is other than `Once`, cascade it to the sibling calls of the same
batch (spec §11, ticket 11 of the spec effort).

## Mechanics

- Discovery: `ctx.Host.ToolCalls` for chat; `AgentTask.ToolCallConfirmationRequests` for a task (the task
  sink exposes the awaiting calls through `IToolCallHost`).
- Three orthogonal axes:
  - **resolution reach** — this call / every awaiting call in the batch;
  - **memory reach** — this tool / same kind (same `(chatId, toolName)`) / every tool;
  - **duration** — `Once` / `Session` / `Task` / `Always`.
- The resolution cascade is a one-off "yes" to the batch: it approves/rejects the siblings once and never
  writes memorization for them. Memorization follows the memory reach only.
- The note lands only on the call being decided.

## UI in this phase

Duration and yes/no come from the existing `ToolConsentPanel`. The reach selector (this tool / same kind /
every tool) has no UI in this pass — the core supports it, the picker is in
[`../not-implemented.md`](../not-implemented.md).

## Acceptance

- Unit test reproduces the worked scenario: `fs-edit`, `fs-edit`, `web-search` awaiting; "remember forever"
  + "answer every call" + note → note only on the decided call, only `fs-edit` memorized, all three
  approved.
- Cancelling one awaiting call does not cancel its siblings.
