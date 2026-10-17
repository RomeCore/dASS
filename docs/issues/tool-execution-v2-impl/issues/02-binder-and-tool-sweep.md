# P2 — ToolMethodBinder and the tool sweep

Status: open
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
- Parameter slots: `IToolExecutionContext`, `[OriginalArgs] JsonNode`, `[SharedContext]`,
  `CancellationToken`, `[Inject]` (resolved from `ctx.Services`), and the JSON arguments. The
  `ReactiveToolResult` slot disappears.
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
