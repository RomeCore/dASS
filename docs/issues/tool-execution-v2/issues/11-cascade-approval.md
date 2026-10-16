# Cascade approval

Status: resolved
Type: grilling
Blocked by: 08

## Question

When the user approves or rejects a tool with a memorization scope other than "just this once", that decision should cascade to other tools that are awaiting approval. This ticket settles the three-way choice in the panel (only this tool, every tool of the same kind, every tool), what "same kind" means, how the cascade reaches tools that are already waiting, and how it interacts with the existing memorization (the in-memory decisions and the persisted "always"). This is feature goal seven.

## Answer

When a decision's memorization scope is other than "just this once", it cascades to the tools already awaiting approval **in the same batch** — discovered through `IToolCallHost.ToolCalls` (the sibling calls of the message; for a task, `AgentTask.ToolCallConfirmationRequests`). This is exactly the seam ticket 04 introduced for the approval helper and cascade.

### Reach and duration are orthogonal

- **Reach** (which tools the decision is *remembered for*): this tool / same kind / every tool. **"Same kind" is the same tool name** — which lines up with the existing `IToolMemorizationService` key `(chatId, toolName)`.
- **Duration**: `Once` / `Session` / `Task` / `Always`.

The **decision reach** (this call / every awaiting call in the batch) is a third, independent axis.

### The worked scenario

`fs-edit`, `fs-edit`, `web-search` are awaiting; the user hits "remember forever" + "answer every call" + a note:

- the note lands only on the tool being decided;
- **only `fs-edit` is remembered**;
- **all three are approved** — the others once-only.

The "answer every call" cascade is a **one-off "yes" to the batch**, not a grant of permanent rights to tools the user never judged. Memorization follows the chosen **memory reach**, never the resolution cascade.

## Comments

<!-- appended conversation -->
