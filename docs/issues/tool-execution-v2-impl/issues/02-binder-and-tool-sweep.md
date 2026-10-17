# P2 — ToolMethodBinder and the tool sweep

Status: resolved
Type: task
Blocked by: 01

## Scope

Collapse the four `*ExecutorCreator` classes and `DelegateAgentTool` onto one `ToolMethodBinder`; make
`ToolInfo` and `AgentTool` **carry** their executors; rename the tool signature to
`IToolExecutionContext`; delete `ReactiveToolResult`; migrate every tool by a sub-agent swarm under
[`../tools-migration-rules.md`](../tools-migration-rules.md).

This is the phase where the tree is expected to be red for a while.

## Binder

```csharp
public static class ToolMethodBinder
{
    public static JsonObject BuildArgumentSchema(MethodInfo method);
    public static IToolMainExecutor             BindMain(object? target, MethodInfo method);
    public static IToolPreviewExecutor          BindPreview(object? target, MethodInfo method);
    public static IToolStreamingAnalyzer        BindStreaming(object? target, MethodInfo method);
    public static IToolSpecifierAnalyzer        BindSpecifier(object? target, MethodInfo method);
    public static IToolSynchronizationAnalyzer  BindSyncAnalyzer(object? target, MethodInfo method);
    public static IToolApprovalProvider         BindApproval(object? target, MethodInfo method);
}
```

- One schema, built once from the main method, reused by every capability.
- Parameter slots: `IToolExecutionContext`, `IToolCall` (a shorthand for `ctx.Call`),
  `[OriginalArgs] JsonNode`, `[SharedContext]`, `CancellationToken`, `[Inject]` (resolved from
  `ctx.Services`), and the JSON arguments. The `ReactiveToolResult` slot disappears.
- Return mapping: `ToolExecutionResult` / `ToolResult` / `string` / `void` and their `Task<>` forms.
- `DelegateMainExecutor` wraps a `Func`; `DelegateAgentTool` becomes an instance of it.

## Carrying

- `ToolInfo` implements `IToolDescriptor`; `ToolInfo.Create` builds the executors through the binder and
  freezes them with the addon. `ToolInfo` gains `StaticSyncKeys`, `HandledDecisions` (ex
  `DefaultSelfHandledDecisions`), `ApprovalHelper`, `ContextFactory`.
- `ToolChange` gains the same overridable metadata; `ToolsetCollector.ApplyChange` copies it.
- `AgentTool` implements `IToolDescriptor`; its `ExecuteAsync`/`PreExecuteAsync`/`AnalyzeSpecifier`
  become carried executors (binder-built from the overridden methods), and `DelegateAgentTool` collapses.
- `ChatAgentTool` is **not** deleted yet — it is dissolved in P5. Until then it delegates to
  `ChatToolInfo.Main`.
- `AgentToolCall`/`ToolCall` already implement `IToolCall` (P1).

## Sweep

Partition by file, rulebook by pattern. Agents: `Implementations/Filesystem`, `Implementations/Memory`,
`Implementations/Web`, `Implementations` root + `MCP`, `Desktop/ToolModules` + Python engine,
`Scripting` (Lua/CSX engines and APIs) + tests.

After the swarm: the lead agent compiles, fixes the reported ambiguities, rewrites the tests that use
`ToolExecutionContext.CreateDummy` / `ReactiveToolResult`, and walks the remaining compiler errors.

## Deletions

`ReactiveToolResult`, `StreamingToolArgumentsAnalysisResult`, the old `PreviewToolExecutionResult`, the
four `*ExecutorCreator` classes, `ToolExecutionContext` (class), `ToolExecutionContext.CreateDummy`,
`ToolInfo.GetExecutableTool` (zero call sites).

Keep `CreateDummy`'s intent only as a **test helper** in the test projects, built on the real
`ChatToolExecutionContext`.

## Acceptance

- Every tool method takes `IToolExecutionContext` and writes into `ctx.Call`.
- `LuaApiTools` still executes tools (its script-source rework lands in P4, but the call compiles and the
  direct `Main` invocation is replaced by the new executor call).
- Desktop modules and the Python engine compile.
- Unit tests for the binder: schema, every slot, return mapping, error cases.

## Answer

### Binder

`Tools/Execution/Executors/ToolMethodBinder.cs` binds all six capabilities and `DelegateMainExecutor`
wraps plain delegates. Recognised slots: `IToolExecutionContext`, `IToolCall` (a shorthand for `ctx.Call`),
`[OriginalArgs] JsonNode`, `[SharedContext]`, `CancellationToken` (only where the capability is
asynchronous), `Specifier` (only in a specifier analyzer), `[Inject]` (resolved from `ctx.Services`), and
the JSON arguments. Return mapping covers `ToolExecutionResult`, `ToolResult`, `string`, `void` and their
`Task<>` forms for the main capability, and the matching leaf type for each other capability.

One correction to the planned API: `Describe` takes the **delegate**, not a bare `MethodInfo`. An instance
method cannot be described without its target, and `ToolInfo.Create` always has the authoring delegate.
The target-taking overload remains public (`Describe(object? target, MethodInfo method)`).

### Carrying

- `ToolInfo` implements `IToolDescriptor` and carries `Main`, `Preview`, `Streaming`, `Specifier`,
  `SyncAnalyzer`, `Approval`, `ContextFactory`, `StaticSyncKeys`, `HandledDecisions`, `ApprovalHelper`.
- `ToolInitializationInfo` exposes the matching delegate slots, including `SyncAnalyzer`, `Approval` and
  `ApprovalRequestedDataSchema`.
- `ToolChange` gained `ApprovalHelper`; `ToolsetCollector.ApplyChange` copies it and stamps the
  chat-scoped `ChatToolContextFactory` on every per-chat clone before the addon is frozen.
- `AgentTool` implements `IToolDescriptor`; its existing abstract methods are adapted onto the carried
  executors. This is the bridge that keeps the task path compiling until P5.
- `ChatAgentTool` still exists as a transitional adapter over a chat `ToolInfo`; P5 dissolves it.

### Sweep

The sub-agent swarm migrated the tool files by partition: Filesystem, Memory, Web, the root modules
(General, Mathematics, Random, Time, Workdir, Skill), the UI-heavy modules (Addon, Agentic, Database,
Forms), the Desktop modules, the scripting engines and their APIs, MCP and the Python engine. The lead
agent finished the two scripting interpreter modules (they were outside the partitions), fixed
`WorkdirToolModule`'s preview type and migrated `SpecifierEngine` to the interface analyzer.

`ToolExecutionService` was rewritten as a transitional inline pipeline over the new domain, executors and
argument accessor. It is deliberately short-lived: P3 extracts the stage services and P4 replaces it with
the orchestrator and dispatcher.

### Deletions

`ReactiveToolResult`, `StreamingToolArgumentsAnalysisResult`, the old `PreviewToolExecutionResult`, the
four `*ExecutorCreator` classes, `ToolExecutionContext` (class, `CreateDummy` included) and
`ToolInfo.GetExecutableTool` are deleted. `Domain.ToolCall.ReactiveToolResult` is gone; the tool call view
model now binds progress and its range directly to the call.

### Tests

`ToolTestContext` builds a real `ChatToolExecutionContext` over a real chat, message and call in both test
projects. `SpecifierEngineTests`, `CSharpScriptMetaEngineTests`, `FilesystemEditToolModuleTests` and
`ShellExecutionToolModuleTests` were migrated to it.

### Gates

- Core project: builds, 0 errors, 0 warnings.
- Desktop project: builds, 0 errors (only the pre-existing NuGet advisory warnings).
- `LLMDesktopAssistant.Tests`: 1175 total, 1174 passed, 1 skipped, 0 failed.
- `LLMDesktopAssistant.Desktop.Tests`: 44 total, 44 passed, 0 failed.
