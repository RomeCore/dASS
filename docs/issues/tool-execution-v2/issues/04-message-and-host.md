# Message and host abstraction

Status: resolved
Type: grilling
Blocked by: 01

## Question

The execution path must stop being tied to the assistant message specifically and work against any chat message that can host a tool call; the agent-task side has its own message type. This ticket settles the abstraction both sides satisfy — what the tool-call host is, what the core actually needs from it, and how the already-broadened message target from the slash-command effort is honoured rather than re-narrowed.

## Answer

The tool-call host is a **minimal neutral interface**:

```csharp
public interface IToolCallHost { IReadOnlyList<IToolCall> ToolCalls { get; } }
```

- **No id.** A message id is a chat concept; it stays on the concrete chat context (`FindMessageId()` lives there, not on the shared contract).
- **Implemented by the base `ChatMessage`** — tool calls already live on `ChatMessage` (not on `AssistantMessage`), which is exactly the broadening the slash-command effort relied on, so it must not be re-narrowed — and by the task's `AgentAssistantMessage`.
- **What the core needs from it:** only the sibling tool calls (used by the approval-helper and cascade). The core works on `(descriptor, call, ctx)` otherwise.
- **What tools need:** the sibling list, and nothing else from the shared host. The owner-agent lookup (`TryGetSenderAgentDescriptor(ctx.Message)`) and any message content (`Message`, `FindMessageId`) are **chat-specific**; a chat tool reaches them by **casting `ctx` to the concrete chat context**, and nothing about them enters the shared contract.

## Comments

<!-- appended conversation -->
