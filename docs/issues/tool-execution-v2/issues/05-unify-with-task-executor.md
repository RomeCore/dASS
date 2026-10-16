# Unifying with the agent-task executor

Status: resolved
Type: grilling
Blocked by: 03

## Question

The agent-task executor currently repeats, almost step for step, the chat execution path: preview, policy, specifiers, approval decision, memorization, consent, execution. This ticket settles how the task path is re-expressed on top of the shared orchestrator and services, what genuinely remains specific to tasks (their own message plumbing, and the confirmation requests surfaced on the task card), and what is deleted from the task executor once the duplication is gone. This is feature goal four.

## Answer

The task path is re-expressed as a **fifth thin orchestrator** (`AgentTaskToolOrchestrator`, Application-scoped — agent tasks are app services that only attach to a chat for dispatching) driving the same eight shared services. The duplicated block inside `AgentTaskExecutor.ExecuteToolAsync` (`preview → policy → specifier → approval → memorization → consent → execution`) is **deleted**. `ChatAgentTool` **dissolves** (a `ToolInfo` is already an `IToolDescriptor`), and `DelegateAgentTool` becomes the same `DelegateMainExecutor` used by chat tools.

What genuinely remains **task-specific**:

1. **Message plumbing** — `AgentAssistantMessage` / `AgentToolCall` / `AgentAttachment` and the lossy projections to and from their native counterparts.
2. **The confirmation sink** — the task card (`AgentTask.ToolCallConfirmationRequests`) as the `IConfirmationSink` implementation.
3. **Parallelism** — `MaxParallelToolCalls` (the orchestrator's semaphore; chat runs unbounded).
4. **Wiring from the launching chat** — `TriggeredChat` / `TriggeredMessage` feed the context factory's provider.
5. **`OnlyResponse` behaviour** — skip tool execution entirely.

Everything else (policy, specifiers, memorization, consent, preview, execution, settlement, streaming, validation, synchronization) is the shared core.

## Comments

<!-- appended conversation -->
