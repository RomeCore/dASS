# P1 — Domain contracts

Status: resolved
Type: task
Blocked by:

## Scope

Add the neutral tool-execution domain (spec §2–§3, Appendix A.1–A.4) **without changing behaviour**. The
old execution path keeps running; the new types are compiled alongside it.

The new subsystem lives under `Tools/Execution/`, and every namespace mirrors its folder:

```
Tools/Execution/Domain/        LLMDesktopAssistant.Tools.Execution.Domain
Tools/Execution/Contexts/      LLMDesktopAssistant.Tools.Execution.Contexts
Tools/Execution/Support/       LLMDesktopAssistant.Tools.Execution.Support
```

## Contracts

- `IToolDescriptor` — identity, schema, metadata (`DefaultExpectedBehaviour`, `ApprovalLevel`,
  `PolicyMask`, specifier block, `StaticSyncKeys`, `HandledDecisions`, `ApprovalHelper`), six carried
  executors (`Main` required), and the carried `ContextFactory`.
- `IToolCall` — the persisted/in-memory state bag: `ToolName`, `Arguments`, `Status`, `ExpectedBehaviour`,
  `ResultContent`, `UseMarkdown`, `StructuredResult`, `StatusIcon`/`StatusTitle`, `UserConfirmationSource`,
  **settable** `CancellationTokenSource`, `AddAttachment`, `SetProgress`, `AddAdditionalData`.
- `IToolCallHost { IReadOnlyList<IToolCall> ToolCalls { get; } }`.
- `IToolExecutionContext` — spec §2.3. `ArgumentStream` is a member of the context; the invocation inputs
  only travel through the factory.
- Executors: `IToolMainExecutor`, `IToolPreviewExecutor`, `IToolStreamingAnalyzer`,
  `IToolSpecifierAnalyzer`, `IToolSynchronizationAnalyzer`, `IToolApprovalProvider`.
- Leaf types: `ToolExecutionResult`, `PreviewToolExecutionResult`, `StreamingToolAnalysisResult`,
  `ToolApprovalResult` — plain sealed classes with init-only properties.
- Support values: `ToolAttachment` + `ToolAttachmentKind`, `ToolSyncKey` + `ToolSyncKeyKind` (readonly
  record struct), `ToolPolicyReason` + `ToolDecisionSource` (readonly record struct),
  `ToolInvocationSource`, `StreamingMode`, `ApprovalHelperConfig` + `ApprovalHelperMode`,
  `IToolArgumentStream`, `IToolArgumentsAccessor`, `IConfirmationSink`.
- `ToolInvocationRequest` — plain class with init-only properties. `Descriptor` is **nullable**; all other
  required members are non-null. No records.
- `IToolExecutionContextFactory` + `ToolContextCreationParams` (plain class, init-only).

## Concrete projections

- `ChatToolExecutionContext : IToolExecutionContext` — carries `Message` and `FindMessageId()`; `Chat` is
  non-null; `Services` is the chat scope; `Consent` is the memorization context. Its `Descriptor` is never
  null (the dispatcher substitutes `MissingToolDescriptor`).
- `AgentTaskExecutionContext : IToolExecutionContext` — `Chat` may be null (`TriggeredChat`); the concrete
  call is `AgentToolCall`.
- `ChatToolContextFactory : IToolExecutionContextFactory` — captures the home `Chat` and the chat
  `IServiceProvider`; stamps chat services.
- `TaskToolContextFactory : IToolExecutionContextFactory` — captures `TriggeredChat?.Services ?? root`.
- `MissingToolDescriptor : IToolDescriptor` — internal to the execution subsystem, name taken from the
  call, empty schema, `ApprovalLevel = AlwaysApprove`, its `Main` writes
  `Error: Tool '<name>' not found.` into `ctx.Call` and returns failure. The dispatcher substitutes it when
  the request has no descriptor, so no stage ever null-checks the descriptor.
- `ToolArgumentsAccessor : IToolArgumentsAccessor` — `Effective = ArgumentsOverride ?? tolerant-parse(raw)`,
  cached by `(overrideReference, rawString)`.
- `ToolCallExtensions` — the sugar from `tools-migration-rules.md`, including `AddAdditionalData`.

## Concrete calls

`Domain.ToolCall` implements `IToolCall` (additive): `CancellationTokenSource` becomes a reactive property
(runtime-only, no LiteDB field), `AddAttachment` projects onto `AdditionalData` as today,
`AddAdditionalData` appends to `AdditionalData`.

`AgentToolCall` implements `IToolCall`: unify its status on `ToolStatus` (delete `AgentToolCallStatus`),
keep `ExpectedBehaviour` aligned with the interface, add the neutral result members. Its
`AddAdditionalData` applies the lossy task rule: `NativeAttachmentMessagePart` → attachment via
`AgentAttachment.TryConvertFromNativeAttachment`, everything else dropped.

## Out of scope

No binder, no stage services, no orchestrators, no deletions. `ReactiveToolResult`, `ToolExecutionContext`
and the four `*Creator` classes stay until P2.

## Acceptance

- Core project compiles with both the old and the new contracts present.
- `Domain.ToolCall` and `AgentToolCall` satisfy `IToolCall`.
- `ToolArgumentsAccessor` has unit tests for override precedence and cache invalidation.
- No behaviour change: the running app still executes tools exactly as before.

## Answer

Landed under `Tools/Execution/`:

- `Domain/` — `IToolDescriptor`, `IToolCall`, `IToolExecutionContext`, `IToolCallHost`, the six executor
  interfaces, the leaf result types, `ToolAttachment`, `ToolSyncKey`, `ToolPolicyReason`,
  `ToolInvocationSource`, `StreamingMode`, `ApprovalHelperConfig`, `IToolArgumentStream`,
  `IToolArgumentsAccessor`, `IConfirmationSink`, `ToolInvocationRequest`, `MissingToolDescriptor`.
- `Contexts/` — `ToolContextCreationParams`, `IToolExecutionContextFactory`, `ChatToolExecutionContext`,
  `AgentTaskExecutionContext`, `ChatToolContextFactory`, `TaskToolContextFactory`.
- `Support/` — `ToolArgumentsAccessor`, `ToolCallExtensions`.

Concrete projections:

- `Domain.ToolCall` implements `IToolCall`. `CancellationTokenSource` and the progress range are reactive
  runtime-only properties; `ExpectedBehaviour` keeps its persisted null and implements the interface
  explicitly. The cancellation source is disposed with the call.
- `AgentToolCall` implements `IToolCall`; its status is unified on `ToolStatus` and
  `AgentToolCallStatus` is deleted. `AddAdditionalData` keeps only a `NativeAttachmentMessagePart`
  (projected through `AgentAttachment.TryConvertFromNativeAttachment`) and drops everything else.
- `ChatMessage` and `AgentAssistantMessage` implement `IToolCallHost`.

Deviations and corrections:

- `ToolContextCreationParams.Host` is `IToolCallHost?`, not the spec's `ChatMessage?`: the task's
  `AgentAssistantMessage` does not derive from `ChatMessage`, and the core only needs the sibling calls.
- The request's `Descriptor` is nullable; the dispatcher substitutes `MissingToolDescriptor`, so
  `IToolExecutionContext.Descriptor` is never null and no stage null-checks it.
- `ToolCallExtensions` mirrors the useful subset of the old reactive surface. There is no completion
  method: the executor task is the completion.

Verification:

- The core project builds with zero warnings.
- Accessor and task-projection tests are added at `tests/LLMDesktopAssistant.Tests/Tools/`.
- A pre-existing stale test was repaired on the way: `SkillArgumentHintParserTests` referenced the
  renamed `ArgumentHintParser` and the old `command.argument.skill.arguments` locale keys; the file is now
  `ArgumentHintParserTests.cs` and matches the current parser.
- The whole `LLMDesktopAssistant.Tests` suite is green: 1175 tests, 1174 passed, 1 skipped
  (the long-standing YamlDotNet colon skip), 0 failed.
