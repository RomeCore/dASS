# Tool execution v2 — specification

> Assembled from the resolved tickets of the `tool-execution-v2` map. This is the implementable document; the tickets hold the reasoning.

## 1. Goal

One tool-execution core shared by both tool worlds (chat and agent tasks), driven by **five thin orchestrators** over a set of **shared stage services**, removing the duplication between the chat execution path and `AgentTaskExecutor`, and delivering the eight feature goals (argument editing, approval/execution separation, richer synchronization, task unification, the approval helper, restart/cancel, cascade approval, Lua hooks).

The shape is **simple decomposition plus orchestrators** — no generic stage engine and no declarative stage list; the nesting is expressed directly in imperative code.

---

## 2. Domain model

### 2.1 `IToolDescriptor` — the description (carries executors, does not implement them)

Implemented by `ToolInfo` (chat/addon) and `AgentTool` (task).

```csharp
public interface IToolDescriptor
{
    string     Name           { get; }
    string     Description    { get; }
    JsonObject ArgumentSchema { get; }

    // declared metadata (overridable per agent via ToolChange)
    ToolBehaviour               DefaultExpectedBehaviour { get; }
    ToolApprovalLevel           ApprovalLevel { get; }
    ToolPolicyMask?             PolicyMask { get; }
    ImmutableList<ToolSpecifierRule> Specifiers { get; }
    SpecifierBehaviourUnionMode?     SpecifierUnionMode { get; }
    SpecifierAggregationMode         SpecifierAggregationMode { get; }
    ImmutableList<string>            SpecifierParameters { get; }
    ImmutableList<ToolSyncKey>       StaticSyncKeys { get; }
    ToolPolicyDecision               HandledDecisions { get; }
    ApprovalHelperConfig?            ApprovalHelper { get; }   // mode, lead/settle delays, extra prompt

    // six carried executors (thin, single-method; only Main is mandatory)
    IToolMainExecutor            Main         { get; }
    IToolPreviewExecutor?        Preview      { get; }
    IToolStreamingAnalyzer?      Streaming    { get; }
    IToolSpecifierAnalyzer?      Specifier    { get; }
    IToolSynchronizationAnalyzer? SyncAnalyzer { get; }
    IToolApprovalProvider?       Approval     { get; }

    // carried, not an executor
    IToolExecutionContextFactory? ContextFactory { get; }
}
```

All declared metadata is overridable per agent through `ToolChange` (applied by `ToolsetCollector.ApplyChange`).

### 2.2 `IToolCall` — the state bag

Implemented by `ToolCall` (chat, **persisted**) and `AgentToolCall` (task, in-memory). Holds **no descriptor**. **Fat but neutral**: chat-native members (`AdditionalData`, …) stay on the concrete `ToolCall`.

Carries: `ToolName`, `Arguments` (string, the model's intent), `Status`, `ExpectedBehaviour`, `ResultContent`, `UseMarkdown`, `StructuredResult`, `StatusIcon`/`StatusTitle`, the confirmation TCS, and a runtime-only per-invocation `CancellationTokenSource` (so the UI can cancel one call). Capability methods: `AddAttachment(ToolAttachment)`, `SetProgress(...)`.

Extension sugar over `IToolCall`: `SetStatusIcon`, `SetStatusTitle`, `UseMarkdown`, `SetStructuredResult`, `SetProgress`, `SetResult`, `MarkSuccess`, `MarkFailed`, `AppendResultLine`.

**`IChatAttachment`** — the single write path. It is a marker implemented by everything a chat object can carry: `AdditionalChatData` (a UI payload — neither branch below), `INativeChatAttachment` (`IAttachment? Native` — the model receives an attachment) and `IContentChatAttachment` (`string Content` — the model receives text appended to the content). `AddAttachment(IChatAttachment)` is the only write method; the call projects the input into the world's native form (an invisible `AdditionalMessageContentPart` / `NativeAttachmentMessagePart` in chat, `AgentContentAttachment` / `AgentNativeAttachment` in a task). Consumers filter by the two branches. `AddAdditionalData` and the old `ToolAttachment` value are gone.

### 2.3 `IToolExecutionContext` — the per-invocation context

```csharp
public interface IToolExecutionContext
{
    IToolCall        Call        { get; }
    IToolDescriptor  Descriptor  { get; }
    IToolCallHost?   Host        { get; }
    object?          SharedContext { get; set; }   // channel between a tool's own stages
    Chat?            Chat        { get; }
    IServiceProvider Services    { get; }
    ToolConsentMemorizationContext? Consent { get; }
    ToolInvocationSource Source  { get; }
    bool             RunningInUi { get; }
    ToolPolicyDecision PolicyDecision { get; }
    ToolPolicyReason?  PolicyReason   { get; }
    ToolPolicyMask   EffectiveBehaviours { get; }
    IToolArgumentStream? ArgumentStream { get; }
    JsonNode?        ArgumentsOverride  { get; set; }
    IToolArgumentsAccessor Arguments { get; }        // .Effective
}
```

The **chat message** (owner agent, message content, `FindMessageId`) is **chat-specific** and lives on the concrete chat context — the shared contract does not carry it. **Every tool signature is renamed `ToolExecutionContext` → `IToolExecutionContext`.**

### 2.4 Results — concrete types; the executor's task is the completion

- `ToolExecutionResult { bool Success }` — main executor's return. No completion handle.
- `PreviewToolExecutionResult { bool? InterruptingSuccess; ToolBehaviour? ExpectedBehaviour }` — payloads go into `ctx.Call`.
- `StreamingToolAnalysisResult { bool StopAnalysis }` — status icon/title go into `ctx.Call`.
- `ToolApprovalResult { bool Approved; ToolApprovalMemorization Memorization; string? Notes }` — carries its own memorization.

**`ReactiveToolResult` is deleted; the old `PreviewToolExecutionResult` class is replaced.** Interfaces remain for the contracts the two worlds implement or DI resolves (descriptor, call, context, executors, services); only the leaf value/result types are concrete.

---

## 3. Executors (six carried capabilities)

```csharp
Task<ToolExecutionResult>        IToolMainExecutor.ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
Task<PreviewToolExecutionResult> IToolPreviewExecutor.PreviewAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
StreamingToolAnalysisResult      IToolStreamingAnalyzer.AnalyzeArgs(JsonNode? partialArgs, IToolExecutionContext ctx);
SpecifierMatchResult             IToolSpecifierAnalyzer.Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx);
IReadOnlyList<ToolSyncKey>       IToolSynchronizationAnalyzer.AnalyzeSyncKeys(JsonNode? args, IToolExecutionContext ctx);
Task<ToolApprovalResult>         IToolApprovalProvider.AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct);
JsonObject?                      IToolApprovalProvider.RequestedDataSchema { get; }
```

Plain polymorphism; **no generics**. Because the descriptor carries pre-built executors, a chat tool runs in a task by handing over its `ToolInfo` unchanged — `ChatAgentTool` dissolves; `DelegateAgentTool` becomes the same `DelegateMainExecutor`.

---

## 4. Shared stage services (eight, all Application-scoped)

| Service | Responsibility |
|---|---|
| `IToolStreamingService` | drain the argument stream, run the optional analyzer → `call.Arguments`, status |
| `IToolValidationService` | tolerant-parse + schema-validate → `ctx.Arguments.Effective` (**synchronous**; the initial implementation is a stub that always returns valid) |
| `IToolSynchronizationService` | keyed higher-order scope around the inner steps (locks live only here) |
| `IToolPreviewService` | run the preview executor, fold the control outcome into the call |
| `IToolPolicyService` | `merge PolicyMask → SpecifierEngine → memorized decision → approve → decision` (specifiers and the pure approver folded in) |
| `IToolConfirmationService` | the confirmation step (explicit + implicit) + memorization + "always" persistence |
| `IToolExecutionService` | run `descriptor.Main` (its task *is* the completion) |
| `IToolSettlementService` | finalize the call (default content, notes, `CompletionToken`), run **always** in a `finally` |

Every stage service is Application-scoped and stateless; all per-chat state flows through `ctx`. Consent machinery survives, re-placed: the pure approver folds into `IToolPolicyService`; `IToolMemorizationService` stays Application-scoped; `ToolConsentPanel` survives as the **universal confirmation shell** (it appears in chat and agent tasks, hosts a tool's custom approval content, and is where the approval helper lives).

---

## 5. Orchestrators & invocation sources

`enum ToolInvocationSource { ChatRun, ChatRestart, Command, AgentTask, Script }` — a plain tag on `ctx.Source`; a per-entry front controller picks the orchestrator.

| Source | Sequence |
|---|---|
| `ChatRun` | streaming (Cyclic) → validation → **sync{** preview → policy → confirmation → execution **}** → settle |
| `ChatRestart` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `Command` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `AgentTask` | streaming (drain only) → validation → sync{ preview → policy → confirmation (implicit) → execution } → settle |
| `Script` | streaming (no-op) → validation → sync{ preview → execution } → settle |

Sequences are **explicit imperative code** per orchestrator — no stage engine, no shared inner helper. Shared is only the stage services and the synchronization higher-order wrapper. Per-source: the assembled `ctx`, `EffectiveBehaviours`, the confirmation sink, the streaming mode, parallelism/cancellation, and the stage set.

### Context creation

`IToolExecutionContextFactory` is an **optional property carried by `IToolDescriptor`** (not a service):

```csharp
public interface IToolExecutionContextFactory { IToolExecutionContext CreateContext(in ToolContextCreationParams parameters); }
public readonly record struct ToolContextCreationParams
{
    public required IToolCall        Call { get; init; }
    public required IToolDescriptor  Descriptor { get; init; }
    public          ToolInvocationSource Source { get; init; }
    public          ChatMessage?     Host { get; init; }
    public          bool             RunningInUi { get; init; }
    public required IConfirmationSink Confirmation { get; init; }
    public          ToolPolicyMask   EffectiveBehaviours { get; init; }
    public          IToolArgumentStream? ArgumentStream { get; init; }
}
```

The provider is the **most-scoped available** (the chat scope for a chat run, `TriggeredChat.Services` for an attached task, the root otherwise); a missing chat-scoped dependency **fails loudly**. The factory is attached on the runtime projection (`Clone → mutate → Freeze`).

---

## 6. Argument streaming

The stream is born in the invocation source (the model's `PartialFunctionToolCall`) and reaches the core as a **neutral `IToolArgumentStream`** on the context. `drain` lives inside the streaming stage, which runs for **every** source (a no-op where there is no stream). In `StreamingMode.Once` the stage drains first, then runs the analyzer once over the complete set; the analyzer runs even without a stream (e.g. a restart). The analyzer itself is optional.

---

## 7. Synchronization

Keys are **static** (declared on the descriptor) plus **per-invocation** (derived by `IToolSynchronizationAnalyzer`, which is the one that absolutizes paths — the synchronizer never resolves them). Keys are **typed** (`File`/`Directory`/`Custom`/`AllFiles`) and **hierarchical** (a held `Directory` conflicts with any `File` beneath it; `AllFiles` conflicts with every file key and with itself, never with `Custom`). Path keys compare OS-aware (case-insensitive on Windows), `Custom` compares ordinally. The synchronizer canonicalizes (trimming trailing separators, dedupe, collapsing a file under a requested directory) and acquires **atomically** — no hold-and-wait, hence no deadlock; conflict detection is prefix-aware.

---

## 8. Approval

Approval is a **core-owned step** (`IToolConfirmationService`), upstream of execution. The tool no longer awaits/applies inside its executor; it contributes a custom approval interface via `IToolApprovalProvider`. The mechanics are gated by the descriptor's `HandledDecisions` mask:

```
decision, reason = policy.Evaluate(ctx)
Approve:  // nothing
Ask:      HandledDecisions.HasFlag(Ask)      ? descriptor.Approval.AwaitDecisionAsync(ctx, ct) : default consent panel
Disallow: HandledDecisions.HasFlag(Disallow) ? descriptor.Approval.AwaitDecisionAsync(ctx, ct) : service writes "denied: {reason}" into the result
memorize from ToolApprovalResult; settle
```

A tool may handle only `Ask`, leaving the `Disallow` mechanics to the service. The decision and its source travel on `ctx`:

```csharp
enum ToolDecisionSource { ApprovalLevel, PolicyMask, Specifier, Memorization, User }
readonly record struct ToolPolicyReason(ToolDecisionSource Source, string? Message);
```

**Outputs flow by nature:** consent → the confirmation service; tool-specific edits (enabled hunks) → `SharedContext`; argument edits → `call.Arguments`; the user's notes → an invisible `AdditionalMessageContentPart` on the call, which the prompt layer appends to the tool result.

---

## 9. Argument editing

Two live channels: **edit `call.Arguments`** (persisted — the model's intent is substituted, at approval) and **set `ctx.ArgumentsOverride`** (ephemeral — the hook channel). Both feed the single **effective-arguments** accessor `IToolArgumentsAccessor.Effective = ArgumentsOverride ?? parsed(call.Arguments)`, which **every stage** reads, cached by `(overrideReference, rawString)` so a stale parse tree is unrepresentable.

**Both channels are validated** against `ArgumentSchema`: approval edits before the confirmation can be accepted; hook overrides **at install** (Lua hooks are not trusted). "Not designed to be edited" is a property of the approval surface, not a flag.

---

## 10. Restart & cancel

A restart re-runs the whole `ChatRestart` sequence on the same `IToolCall` (state reset), optionally with edited arguments (→ `call.Arguments`); resuming from a chosen stage is rejected. Cancellation is per-invocation: each `IToolCall` carries its own `CancellationTokenSource` (runtime-only) linked to the run token; cancelling one tool leaves the run and siblings alive; a tool awaiting approval is cancelled via its consent; produced output is kept.

---

## 11. Cascade approval

A decision whose scope is other than once cascades to the **sibling awaiting calls of the same batch** — via `IToolCallHost.ToolCalls` (tasks: `AgentTask.ToolCallConfirmationRequests`). Reach (this tool / same name / every tool — "same kind" = same tool name, matching the existing `(chatId, toolName)` key) and duration (`Once`/`Session`/`Task`/`Always`) are **orthogonal**, as is the resolution reach. The resolution cascade is a one-off "yes", so memorization follows the memory reach, never the cascade.

---

## 12. Approval helper

> **Deferred.** The tool system is implemented **first without the helper**; the helper — and its rendering — land after that. Everything else in this spec does not depend on it.

Lives inside `ToolConsentPanel`; not a pipeline step. Mode (`manual`/`assistant`/`worker`) is a per-tool setting (descriptor + `ToolChange` override path) with `LeadDelay`/`SettleDelay`/`ExtraPrompt`; the model is configurable. Context = descriptor + effective arguments + decision/`ToolPolicyReason` + preview artifact + recent messages (via `MessageQuoteRenderer`) + `ExtraPrompt`.

Output:

```jsonc
{
  "verdict": "approve" | "disallow" | "not_enough_context",
  "notes_for_user":  "…",
  "notes_for_agent": "…",
  "requested_data":  { … },     // per the tool's RequestedDataSchema
  "override_agent_intent": true,// true → call.Arguments (persisted); false → ArgumentsOverride
  "new_arguments":   { … }
}
```

`not_enough_context` escalates even in `worker`; the two notes are split; `new_arguments` and `requested_data` are **mutually exclusive per tool**; `override_agent_intent` picks ticket 09's channel.

---

## 13. Trust model for non-agent invocations

- **Agent decisions** (`ChatRun`, `AgentTask`) — full pipeline.
- **The user's own actions** (`Command`, `ChatRestart`) — skip policy/confirmation, honour the **hard floor** (`AlwaysDisallow`).
- **Scripts** (`Script`, nested Lua/CSX/Python calls) — **bypass the pipeline entirely** (no policy, no confirmation, no hard floor) — a deliberately powerful area.

---

## 14. Hooks (further expansion)

Hook points are **positional** (`BeforeX`/`AfterX`) around the stages, fired by the owning stage service; they are **hooks, not events** (the chat-execution hook list is deliberately not the model). Execution: sequential, each sees the previous result, can interrupt/override (a hook beats the policy). Deferred: point taxonomy, verdict shape, ordering, failure handling, sequential-vs-parallel.

---

## 15. Tool-command source (slash → tool)

A **derived slash command** like skills/sub-agents: `DerivedSlashCommandProvider<ToolInfo>` owns the source (`/tool:<name>`, derived tier), the argument schema is generated from the tool's `ArgumentSchema` (designated main argument → the positional/rest slot), and the run is the `Command` orchestrator (user action — policy/confirmation skipped, hard floor honoured).

---

## 16. Task unification

The task tool path is the fifth orchestrator (`AgentTaskToolOrchestrator`, App-scoped); the duplicated block inside `AgentTaskExecutor.ExecuteToolAsync` is deleted. Task-specific remainder: message plumbing (`AgentAssistantMessage`/`AgentToolCall`/`AgentAttachment` + lossy projections), the task-card confirmation sink, `MaxParallelToolCalls`, chat-trigger wiring, `OnlyResponse`.

---

## 17. Authoring consolidation

The four `*ExecutorCreator` classes and `DelegateAgentTool` collapse onto one **`ToolMethodBinder`** (reflection/argument binding); `DelegateAgentTool` becomes an instance of the same `DelegateMainExecutor` used by chat tools.

---

## 18. Out of scope

- **Implementation and build** — this spec is the destination; writing the code is a separate effort.
- Anything **beyond the tool-execution subsystem** (prompt pipeline, memory, model management). In particular, projecting a tool call's `AdditionalMessagePart`s into the tool-message content via `AgentPromptComposer` / `MessageQuoteRenderer` is a **prompt-layer** task, out of scope here.
- Comparisons with other agent runtimes.

---

# Appendix A — Concrete contracts

## A.1 Domain

```csharp
public interface IToolDescriptor
{
    string     Name           { get; }
    string     Description    { get; }
    JsonObject ArgumentSchema { get; }

    ToolBehaviour                    DefaultExpectedBehaviour { get; }
    ToolApprovalLevel                ApprovalLevel { get; }
    ToolPolicyMask?                  PolicyMask { get; }
    ImmutableList<ToolSpecifierRule> Specifiers { get; }
    SpecifierBehaviourUnionMode?     SpecifierUnionMode { get; }
    SpecifierAggregationMode         SpecifierAggregationMode { get; }
    ImmutableList<string>            SpecifierParameters { get; }
    ImmutableList<ToolSyncKey>       StaticSyncKeys { get; }
    ToolPolicyDecision               HandledDecisions { get; }
    ApprovalHelperConfig?            ApprovalHelper { get; }

    IToolMainExecutor             Main          { get; }   // required
    IToolPreviewExecutor?         Preview       { get; }
    IToolStreamingAnalyzer?       Streaming     { get; }
    IToolSpecifierAnalyzer?       Specifier     { get; }
    IToolSynchronizationAnalyzer? SyncAnalyzer  { get; }
    IToolApprovalProvider?        Approval      { get; }

    IToolExecutionContextFactory? ContextFactory { get; }
}
```

```csharp
public interface IToolCall
{
    // persisted
    string            ToolName          { get; }
    string            Arguments         { get; set; }   // the model's intent
    ToolStatus        Status            { get; set; }
    ToolBehaviour     ExpectedBehaviour { get; set; }
    string?           ResultContent     { get; set; }
    bool              UseMarkdown       { get; set; }
    JsonNode?         StructuredResult  { get; set; }
    VisualIconKind?   StatusIcon        { get; set; }
    string?           StatusTitle       { get; set; }

    // runtime-only (never serialized)
    TaskCompletionSource<ToolConsentResult>? UserConfirmationSource { get; set; }
    CancellationTokenSource                  CancellationTokenSource { get; }

    // capability methods
    void AddAttachment(IChatAttachment attachment);
    void SetProgress(double? value, double min, double max);
}

public interface IToolCallHost { IReadOnlyList<IToolCall> ToolCalls { get; } }
```

```csharp
public interface IToolExecutionContext
{
    IToolCall        Call        { get; }
    IToolDescriptor  Descriptor  { get; }
    IToolCallHost?   Host        { get; }                    // the message's sibling tool calls
    IConfirmationSink Confirmation { get; }                  // the invoking world's confirmation surface
    object?          SharedContext { get; set; }             // a tool's own channel between its stages
    Chat?            Chat        { get; }
    IServiceProvider Services    { get; }
    ToolConsentMemorizationContext? Consent { get; }
    ToolInvocationSource Source  { get; }
    bool             RunningInUi { get; }

    ToolPolicyDecision PolicyDecision { get; set; }          // pipeline-owned
    ToolPolicyReason?  PolicyReason   { get; set; }          // pipeline-owned
    ToolPolicyMask     EffectiveBehaviours { get; }          // set at creation from the source

    IToolArgumentStream? ArgumentStream { get; }
    JsonNode?            ArgumentsOverride { get; set; }     // hook channel
    IToolArgumentsAccessor Arguments { get; }                // .Effective
}
```

## A.2 Executors

```csharp
public interface IToolMainExecutor
{ Task<ToolExecutionResult> ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct); }

public interface IToolPreviewExecutor
{ Task<PreviewToolExecutionResult> PreviewAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct); }

public interface IToolStreamingAnalyzer
{ StreamingToolAnalysisResult AnalyzeArgs(JsonNode? partialArgs, IToolExecutionContext ctx); }

public interface IToolSpecifierAnalyzer
{ SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx); }

public interface IToolSynchronizationAnalyzer
{ IReadOnlyList<ToolSyncKey> AnalyzeSyncKeys(JsonNode? args, IToolExecutionContext ctx); }

public interface IToolApprovalProvider
{
    JsonObject? RequestedDataSchema { get; }
    Task<ToolApprovalResult> AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct);
}
```

## A.3 Leaf (concrete) types

```csharp
public sealed class ToolExecutionResult         { public required bool Success { get; init; } }
public sealed class PreviewToolExecutionResult  { public bool? InterruptingSuccess { get; init; }
                                                  public ToolBehaviour? ExpectedBehaviour { get; init; } }
public sealed class StreamingToolAnalysisResult { public bool StopAnalysis { get; init; } }
public sealed class ToolApprovalResult          { public required bool Approved { get; init; }
                                                  public required ToolApprovalMemorization Memorization { get; init; }
                                                  public string? Notes { get; init; } }
```

## A.4 Support

```csharp
public enum AttachmentKind { Image, Audio, Video }
public interface IChatAttachment;                                    // a payload addable to a chat object
public interface INativeChatAttachment : IChatAttachment { IAttachment? Native { get; } }
public interface IContentChatAttachment : IChatAttachment { string Content { get; } }
public sealed class ChatNativeAttachment : INativeChatAttachment { /* (IAttachment) or (AttachmentKind, url) */ }
public sealed class ChatContentAttachment : IContentChatAttachment { /* (string content) */ }

public enum ToolSyncKeyKind { File, Directory, Custom, AllFiles }
public readonly record struct ToolSyncKey(ToolSyncKeyKind Kind, string Value);

public enum ToolDecisionSource { ApprovalLevel, PolicyMask, Specifier, Memorization, User }
public readonly record struct ToolPolicyReason(ToolDecisionSource Source, string? Message);

public enum StreamingMode { Cyclic, Once }
public enum ToolInvocationSource { ChatRun, ChatRestart, Command, AgentTask, Script }

public interface IToolArgumentStream
{
    string AccumulatedArgs { get; }
    IDisposable OnArgsUpdated(Action handler);
    Task Drained { get; }                       // resolves when the model stopped emitting
}

public interface IToolArgumentsAccessor { JsonNode? Effective { get; } }

public interface IConfirmationSink
{ Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct); }

public enum ApprovalHelperMode { Manual, Assistant, Worker }
public sealed class ApprovalHelperConfig
{
    public ApprovalHelperMode Mode        { get; init; }
    public TimeSpan           LeadDelay   { get; init; }
    public TimeSpan           SettleDelay { get; init; }
    public string?            ExtraPrompt { get; init; }
}

public interface IToolExecutionContextFactory
{ IToolExecutionContext CreateContext(in ToolContextCreationParams parameters); }

public readonly record struct ToolContextCreationParams
{
    public required IToolCall        Call        { get; init; }
    public required IToolDescriptor  Descriptor  { get; init; }
    public          ToolInvocationSource Source  { get; init; }
    public          ChatMessage?     Host        { get; init; }
    public          bool             RunningInUi { get; init; }
    public required IConfirmationSink Confirmation { get; init; }
    public          ToolPolicyMask   EffectiveBehaviours { get; init; }
    public          IToolArgumentStream? ArgumentStream { get; init; }
}
```

## A.5 Stage services

```csharp
public interface IToolStreamingService       { Task RunAsync(IToolExecutionContext ctx, StreamingMode mode, CancellationToken ct); }
public interface IToolValidationService      { bool Validate(IToolExecutionContext ctx); }
public interface IToolSynchronizationService { Task RunAsync(IToolExecutionContext ctx, Func<Task> inner, CancellationToken ct); }
public interface IToolPreviewService         { Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct); }      // false = interrupted
public interface IToolPolicyService          { Task<ToolPolicyDecision> EvaluateAsync(IToolExecutionContext ctx, CancellationToken ct); }
public interface IToolConfirmationService    { Task<bool> ConfirmAsync(IToolExecutionContext ctx, CancellationToken ct); } // true = approved
public interface IToolExecutionService       { Task RunAsync(IToolExecutionContext ctx, CancellationToken ct); }
public interface IToolSettlementService      { void Finalize(IToolExecutionContext ctx); }
```

All `[Service(typeof(IX))]` (Application singleton).

## A.6 Authoring

```csharp
// builds one carried executor per capability, once, at descriptor-construction time
public static class ToolMethodBinder
{
    public static JsonObject BuildArgumentSchema(MethodInfo method);            // from the main method
    public static IToolMainExecutor             BindMain(object? target, MethodInfo method);
    public static IToolPreviewExecutor          BindPreview(object? target, MethodInfo method);
    public static IToolStreamingAnalyzer        BindStreaming(object? target, MethodInfo method);
    public static IToolSpecifierAnalyzer        BindSpecifier(object? target, MethodInfo method);
    public static IToolSynchronizationAnalyzer  BindSyncAnalyzer(object? target, MethodInfo method);
    public static IToolApprovalProvider         BindApproval(object? target, MethodInfo method);
}

public sealed class DelegateMainExecutor : IToolMainExecutor { /* wraps a Func; also used for AgentTool */ }
```

Special parameters the binder recognises (as today): `IToolExecutionContext`, `[OriginalArgs] JsonNode`, `[SharedContext]`, `CancellationToken`, `[Inject]`, plus per-capability slots (prepared result for main, `Specifier` for the specifier analyzer). Schema is built **once** from the main method and reused by the other capabilities.

## A.7 Concrete implementations (chat + task)

```csharp
public sealed class ChatToolExecutionContext : IToolExecutionContext
{
    public ChatMessage Message { get; }        // chat-specific; reached by a chat tool via cast
    public int FindMessageId();
    // Chat, Services = chat scope, Consent = ToolConsentMemorizationContext
}
public sealed class AgentTaskExecutionContext : IToolExecutionContext { /* Chat = TriggeredChat (nullable) */ }

public sealed class ChatToolContextFactory : IToolExecutionContextFactory { /* captures the home Chat; Services = chat.Services */ }
public sealed class TaskToolContextFactory : IToolExecutionContextFactory { /* Services = TriggeredChat?.Services ?? root */ }

public sealed class ChatConfirmationSink : IConfirmationSink { /* drives ToolConsentPanel via ctx.Call.UserConfirmationSource */ }
public sealed class TaskConfirmationSink : IConfirmationSink { /* drives ToolConsentPanel via AgentTask.ToolCallConfirmationRequests */ }
```

---

# Appendix B — Current → new map

### Deleted
`ReactiveToolResult`; the old `PreviewToolExecutionResult` class (replaced by the new concrete one); `StreamingToolArgumentsAnalysisResult` (→ `StreamingToolAnalysisResult`); `ChatAgentTool`; `DelegateAgentTool`; `ToolExecutionService`; the four `*ExecutorCreator` classes; the duplicated tool block inside `AgentTaskExecutor.ExecuteToolAsync`; `ToolAttachment` / `ToolAttachmentKind`; `AgentAttachmentType`; `IToolCall.AddAdditionalData`; `CompletionToken` on `ToolCall` and `AssistantMessage`.

### Reshaped
| Current | New |
|---|---|
| `ToolInfo` (delegates `Executor`/`PreviewExecutor`/`StreamingAnalyzer`/`SpecifierAnalyzer`) | implements `IToolDescriptor`; the delegates become **carried executors** built by `ToolMethodBinder` |
| `AgentTool` (abstract `ExecuteAsync`/`PreExecuteAsync`/`AnalyzeSpecifier`) | implements `IToolDescriptor`; its methods become carried executors |
| `ToolExecutionContext` | `IToolExecutionContext` (interface) + concrete `ChatToolExecutionContext`; **every tool signature renamed** |
| `ToolCall` / `AgentToolCall` | implement `IToolCall` |
| `ToolInfo.SynchronizationGroup` | `IToolDescriptor.StaticSyncKeys` |
| `DefaultSelfHandledDecisions` | `HandledDecisions` |
| `IToolApprovalService.ApproveTool` | folded into `IToolPolicyService` |
| `AgentAttachment` | `AgentNativeAttachment` / `AgentContentAttachment`, both behind `IChatAttachment` |
| `ToolChange` | gains the new overridable metadata (helper config, …) |
| `ToolsetCollector` | `ApplyChange` copies the new metadata as it does today |

### Added
`IToolDescriptor`, `IToolCall`(interface), `IToolExecutionContext`, `IToolCallHost`, the six executor interfaces, `IToolExecutionContextFactory` + `ToolContextCreationParams`, `IChatAttachment`/`INativeChatAttachment`/`IContentChatAttachment`/`AttachmentKind`, `ToolSyncKey`, `IToolArgumentStream`, `IToolArgumentsAccessor`, `ToolPolicyReason`/`ToolDecisionSource`, `ApprovalHelperConfig`/`ApprovalHelperMode`, `IConfirmationSink`, the eight stage services, the `ToolMethodBinder` + `DelegateMainExecutor`, the five orchestrators (`ChatToolOrchestrator`, `ChatRestartOrchestrator`, `ToolCommandOrchestrator`, `AgentTaskToolOrchestrator`, `ScriptToolOrchestrator`), `ToolExecutionResult`, `StreamingToolAnalysisResult`, `ToolApprovalResult`, `DerivedSlashCommandProvider<ToolInfo>`.

### Kept as-is
`ToolApprovalLevel`, `ToolPolicyMask`, `ToolPolicyDecision`, `ToolBehaviour` + `ToolBehaviourCategoryClassifier`, `SpecifierEngine`, `IToolMemorizationService`, `ToolConsentPersister`, `ToolConsentMemorizationContext`, `ToolsetCollector`, `DerivedSlashCommandProvider<T>`, the `SlashCommand*` subsystem, `ToolConsentPanel` (extended), `AgentTask`/`AgentTaskExecutor` (tool block removed).

---

# Appendix C — Per-stage value flow (chat run)

| Stage | Reads | Writes |
|---|---|---|
| **streaming** | `ctx.ArgumentStream`, `descriptor.Streaming` | `call.Arguments`, `call.StatusIcon`/`StatusTitle`, (`ctx.SharedContext`) |
| **validation** | `call.Arguments`, `descriptor.ArgumentSchema` | `ctx.Arguments` (parsed), throws/`false` on invalid |
| **sync** | `descriptor.StaticSyncKeys`, `descriptor.SyncAnalyzer`, `ctx.Arguments.Effective` | holds the keyed scope around the inner steps |
| **preview** | `descriptor.Preview`, `ctx.Arguments.Effective` | `call.Status`, `call.ExpectedBehaviour`, `ctx.SharedContext`, payload into `call`; returns `false` to interrupt |
| **policy** | `ctx.EffectiveBehaviours`, `descriptor.PolicyMask`/`Specifiers`/`Specifier`/`ApprovalLevel`, memorization | `ctx.PolicyDecision`, `ctx.PolicyReason` |
| **confirmation** | `ctx.PolicyDecision`/`PolicyReason`, `descriptor.HandledDecisions`/`Approval`, `ctx.Host` | `call.Status`, `call.UserConfirmationSource`, memorization, `ctx.Consent`; returns approved |
| **execution** | `descriptor.Main`, `ctx.Arguments.Effective`, `ctx.SharedContext` | `call.Status`/`ResultContent`/… , returns `ToolExecutionResult` |
| **settlement** | `call.Status`, `call.ResultContent` | default content (the caller owns its completion state) |

`ctx.Arguments.Effective = ArgumentsOverride ?? parsed(call.Arguments)` — recomputed when either input changes.

---

# Appendix D — Migration order

1. **Leaf types + contracts** — add `ToolExecutionResult`, `StreamingToolAnalysisResult`, `ToolAttachment`, `ToolSyncKey`, `ToolPolicyReason`, `IToolMainExecutor`…`IToolApprovalProvider`, `IToolDescriptor`, `IToolCall`, `IToolExecutionContext` (no behaviour change yet).
2. **`ToolMethodBinder`** — collapse the four `*Creator`s + `DelegateAgentTool` onto it; `ToolInfo`/`AgentTool` now **carry** executors.
3. **Context split** — `IToolExecutionContext` + concrete chat/task contexts + `IToolExecutionContextFactory`; rename all tool signatures (option A); `ToolConsentPanel` becomes the universal shell.
4. **Stage services** — extract the eight from `ToolExecutionService`; make them Application-scoped, state via `ctx`.
5. **Orchestrators** — introduce the five; delete `ToolExecutionService`.
6. **Task path** — `AgentTaskToolOrchestrator`; delete the duplicated block in `AgentTaskExecutor`.
7. **Results** — delete `ReactiveToolResult`; tools write into `ctx.Call`; extension helpers.
8. **Approval** — confirmation step + `IToolApprovalProvider`; arg editing (two channels + accessor); cascade.
9. **Restart/cancel** — per-call CTS.
10. **Tool-command source** — `DerivedSlashCommandProvider<ToolInfo>`.
11. **Approval helper** — a **later phase**, after the tool system works without it.
12. **Hooks** — positional interception points (deferred scope).
