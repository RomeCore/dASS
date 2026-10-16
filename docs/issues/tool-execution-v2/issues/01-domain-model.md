# Domain model of the execution core

Status: resolved
Type: grilling
Blocked by:

## Question

The execution core needs a single domain model that both the chat tool world and the agent-task tool world project onto: a tool call, a tool descriptor, and a tool result that belong to neither world's concrete types. This ticket settles what those three domain concepts are and what each one carries; how the existing concrete types on both sides become projections onto them; where the metadata currently duplicated across the two worlds ends up (expected behaviour, approval level, policy mask, specifiers and their modes); and whether the core speaks through plain polymorphism or through generics. The chat-only richness — live progress, streaming status, appended additional data, custom result views — must not leak into the shared core, yet the chat path must still reach it. This is the root of the whole effort: everything else projects onto what is settled here.

## Answer

The core is described by **two domain contracts plus a per-invocation context**. There is no third "tool result" domain concept.

### 1. `IToolDescriptor` — the description (carries its executors, does not implement them)

Implemented by `ToolInfo` (chat/addon) and `AgentTool` (agent task). It holds identity, the JSON argument schema, the declared policy metadata (`DefaultExpectedBehaviour`, `ApprovalLevel`, `PolicyMask`, specifier rules + union/aggregation modes + parameters, static sync keys, `HandledDecisions`, and the approval-helper config — mode, lead/settle delays, extra prompt; all of these are overridable per agent through `ToolChange`), and — as **carried objects** — its **six executors** (all thin, single-method; only `Main` is mandatory):

```csharp
IToolMainExecutor       Main          { get; }     // required
IToolPreviewExecutor?   Preview       { get; }
IToolStreamingAnalyzer? Streaming     { get; }     // chat-only
IToolSpecifierAnalyzer? Specifier     { get; }
IToolSynchronizationAnalyzer? SyncAnalyzer { get; }  // ticket 06
IToolApprovalProvider?  Approval      { get; }     // ticket 08

IToolExecutionContextFactory? ContextFactory { get; }   // carried, not an executor (ticket 02)
```

Chat tools supply adapters over the existing delegates (`ToolExecutorCreator`); task tools supply their own. Because the descriptor carries pre-built executors, a chat tool runs in a task by handing over its `ToolInfo` descriptor unchanged — the `ChatAgentTool` adapter dissolves. Executor signatures:

```csharp
Task<ToolExecutionResult>        IToolMainExecutor.ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
Task<PreviewToolExecutionResult> IToolPreviewExecutor.PreviewAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
StreamingToolAnalysisResult     IToolStreamingAnalyzer.AnalyzeArgs(JsonNode? partialArgs, IToolExecutionContext ctx);          // sync
SpecifierMatchResult            IToolSpecifierAnalyzer.Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx); // sync
IReadOnlyList<ToolSyncKey>      IToolSynchronizationAnalyzer.AnalyzeSyncKeys(JsonNode? args, IToolExecutionContext ctx); // sync
Task<ToolApprovalResult>        IToolApprovalProvider.AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct);
```

Plain polymorphism; **no generics**.

### 2. `IToolCall` — the state bag

Implemented by `ToolCall` (chat, **persisted**) and `AgentToolCall` (task, in-memory). It holds **no descriptor**. It carries the neutral superset of state (tool name, arguments, status, expected behaviour, result content, markdown flag, structured result, status icon/title, the confirmation TCS, and a runtime-only per-invocation `CancellationTokenSource`), plus attachments and progress exposed through **neutral capability methods** (`AddAttachment(ToolAttachment)`, `SetProgress`). `ToolAttachment` is the neutral attachment value — the promoted `AgentAttachment` (`{ ToolAttachmentKind Kind; string Url }`; url already carries base64) — an *argument* of `AddAttachment`, never stored state. The contract is **fat but neutral**: chat-native members (`AdditionalData`, `ReactiveToolResult`) live on the concrete `ToolCall` and are never named by the shared core. The *effective* arguments live on the context, not the call.

### 3. `IToolExecutionContext` — the per-invocation context

A single interface that the tools and the shared services both take. It carries `Call` (`IToolCall`), `Descriptor`, `SharedContext`, `Chat?`, `Services`, consent context, source, `RunningInUI`, the decision (`PolicyDecision` + `PolicyReason`), the per-invocation inputs (`EffectiveBehaviours`, `ArgumentStream`, `ArgumentsOverride`), and the derived effective-arguments accessor. The **chat message** (owner-agent, message content, `FindMessageId`) is **chat-specific** and lives on the concrete chat context — the shared contract does **not** carry it (ticket 04). **Every tool signature is renamed `ToolExecutionContext` → `IToolExecutionContext`** as part of the refactor.

### 4. Results are concrete types; the executor's task is the completion

- **`ToolExecutionResult`** (concrete) — `{ bool Success }`. There is no completion handle: everything mutates `IToolCall` directly, and the executor's own task *is* the completion (`var result = await Main.ExecuteAsync(...)`).
- **`PreviewToolExecutionResult`** (concrete) — `{ bool? InterruptingSuccess; ToolBehaviour? ExpectedBehaviour }` only. Content and payloads are written straight into `ctx.Call`.
- **`StreamingToolAnalysisResult`** (concrete) — `{ bool StopAnalysis { get; init; } }`; the streaming analyzer writes any status icon/title into `ctx.Call`.
- **`ToolApprovalResult`** (concrete) — `{ bool Approved; ToolApprovalMemorization Memorization; string? Notes }` — the decision **carries its own memorization**.

Interfaces are kept for the **contracts the two worlds implement or DI resolves** — the descriptor, the call, the context, the six executors, and the stage services. Only the leaf value/result types are concrete.

Consequences:
- **`ReactiveToolResult` is deleted and the old `PreviewToolExecutionResult` class is replaced** (option A). Tools write their output straight into `ctx.Call`.
- The output payloads (`AdditionalChatData` in chat, `AgentAttachment` in tasks) **never enter the shared return value**; they are written into the world's own call and projected at the world boundary (lossily, when a chat tool runs in a task). This is how the `AdditionalChatData` / `AgentAttachment` divergence is resolved — by keeping payloads out of the shared contract entirely.
- Convenience is preserved as an **extension class** over `IToolCall` (`SetStatusIcon`, `SetStatusTitle`, `UseMarkdown`, `SetStructuredResult`, `SetProgress`, `SetResult`, `MarkSuccess`, …) rather than as a wrapper type.
- The tool no longer overrides the pipeline decision: the self-handled mechanism becomes the descriptor's `HandledDecisions` filter, consumed by the confirmation stage (ticket 08).

### 5. Argument streaming

The argument stream is born in the invocation source (the model's `PartialFunctionToolCall`) and reaches the core as a **neutral `IToolArgumentStream`** carried on the context. The streaming stage runs for **every** source; `drain` (awaiting the full argument set) lives inside it and is a no-op where there is no stream (restart, command, script). In `StreamingMode.Once` the stage drains first and runs the analyzer once over the complete argument set; the analyzer runs even when no stream is present (e.g. a restart), using the already-known arguments. The streaming analyzer is an optional descriptor capability.

### 6. Authoring consolidation

The four `*ExecutorCreator` classes and `DelegateAgentTool` share ~95% of their reflection/argument-binding code; they are collapsed onto one **`ToolMethodBinder`**, and `DelegateAgentTool` becomes an instance of the same `DelegateMainExecutor` used by chat tools.

### Premise kept

Chat-only richness does not leak into the shared core; the chat path still reaches it through the concrete `ToolCall` and the source-specific seams.

## Comments

<!-- appended conversation -->
