# P5 — Agent-task path

Status: open
Type: task
Blocked by: 04

## Scope

Re-express the task tool path as `AgentTaskToolOrchestrator` (Application-scoped) over the shared
services (spec §16, ticket 05 of the spec effort).

## What changes

- Delete the duplicated `preview → policy → specifier → approval → memorization → consent → execution`
  block inside `AgentTaskExecutor.ExecuteToolAsync`.
- `AgentTaskExecutor` keeps: message plumbing (`AgentAssistantMessage` / `AgentToolCall` /
  `AgentAttachment` and the lossy projections), `MaxParallelToolCalls`, chat-trigger wiring,
  `OnlyResponse`, and the loop that turns tool results into `ToolMessage`s.
- `AgentTaskToolOrchestrator` runs `streaming (drain only, no analyzer) → validation → sync{ preview →
  policy → confirmation (implicit) → execution } → settle` with `Source = AgentTask`.
- `TaskConfirmationSink` drives `AgentTask.ToolCallConfirmationRequests` and the task card.
- `ChatAgentTool` **dissolves**: the task toolset becomes `ImmutableDictionary<string, IToolDescriptor>`
  (or a list) and receives a chat `ToolInfo` directly. A small projection helper
  (`Clone → stamp TaskToolContextFactory → Freeze`) is used by `AgenticToolModule`, `SubAgentToolResolver`
  and `LuaApiAgents`.
- `DelegateAgentTool` uses the shared `DelegateMainExecutor`.
- `MemorizeAlwaysInToolset` moves into the confirmation stage / chat-scoped sink path, not the task
  executor.

## Acceptance

- `ChatAgentTool` is gone; no wrapper exists between a chat tool and a task.
- An agent task using a chat tool produces the same user-visible behaviour as today (result on the task
  card, confirmation on the card, attachments projected).
- `OnlyResponse` still skips execution entirely.
- `MaxParallelToolCalls` still bounds parallelism.
