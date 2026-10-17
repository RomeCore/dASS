# Tool sweep — hard rules

These rules are handed verbatim to every sub-agent of the P2 sweep. They are mechanical: an agent that
has to make a judgement call stops and reports the file instead of inventing a shape.

## What an agent may change

Only the files assigned to it. Only the patterns below. No new public types, no renames outside the
mapping, no "while I was here" cleanups, no formatting sweeps.

If a file matches none of the patterns, or a pattern does not fit, leave the file untouched and add it to
the report with the reason. The lead agent fixes those by hand.

## Type mapping

| Before | After |
|---|---|
| `ToolExecutionContext` (class) | `IToolExecutionContext` (interface) |
| `ReactiveToolResult` | deleted; write into `ctx.Call` and return `ToolExecutionResult` |
| `ReactiveToolResult result` parameter | deleted; the method uses `ctx.Call` (add `IToolExecutionContext ctx` if absent) |
| `Task<ReactiveToolResult>` | `Task<ToolExecutionResult>` |
| `StreamingToolArgumentsAnalysisResult` | `StreamingToolAnalysisResult` |
| `PreviewToolExecutionResult` (old class) | `Tools.Execution.Domain.PreviewToolExecutionResult` |
| `ToolInfo.DefaultSelfHandledDecisions` | `ToolInfo.HandledDecisions` |
| `ToolInfo.SynchronizationGroup` (string) | `ToolInfo.StaticSyncKeys` (`ToolSyncKey(Kind: Custom, Value: <old string>)`) |
| `AgentAttachment` as a tool payload | `ToolAttachment` (neutral); `AgentAttachment` survives only as the task's projection |

## Member mapping on the call

| Before (`ReactiveToolResult` / result object) | After (via `ToolCallExtensions`) |
|---|---|
| `result.ResultContent = x` | `ctx.Call.ResultContent = x` |
| `result.ResultContentLines.Add(x)` | `ctx.Call.AppendResultLine(x)` |
| `result.ResultContentLines.ToList()` | `ctx.Call.GetResultLines()` |
| `result.Progress = p` | `ctx.Call.SetProgress(p)` |
| `result.MinProgress` / `MaxProgress` | `ctx.Call.SetProgress(p, min, max)` |
| `result.StatusIcon = i` | `ctx.Call.SetStatusIcon(i)` |
| `result.StatusTitle = t` | `ctx.Call.SetStatusTitle(t)` |
| `result.UseMarkdown = true` | `ctx.Call.UseMarkdown = true` |
| `result.StructuredResult = j` | `ctx.Call.SetStructuredResult(j)` |
| `result.AdditionalData.Add(part)` | `ctx.Call.AddAdditionalData(part)` |
| `result.Complete(true/false)` | delete — the executor's task is the completion |
| `result.TryCompleteWithSuccess/Error()` | delete — return `ToolExecutionResult.Success/Failure` |
| `ReactiveToolResult.CreateSuccess(s)` | `ctx.Call.SetResult(s); return ToolExecutionResult.Success` |
| `ReactiveToolResult.CreateError(s)` | `ctx.Call.SetResult(s); return ToolExecutionResult.Failure` |
| `ReactiveToolResult.Create(success, s)` | same, with the matching result |
| `new ReactiveToolResult { ... }` | writes to `ctx.Call` + a `ToolExecutionResult` return |

## Method return mapping

The binder accepts only these shapes:

- `ToolExecutionResult`, `Task<ToolExecutionResult>`
- `ToolResult`, `Task<ToolResult>`
- `string`, `Task<string>`
- `void`, `Task`

`ReactiveToolResult` is no longer a valid return type. A method that used the prepared-result parameter
(`Task` + `ReactiveToolResult result`) becomes a normal `async Task<ToolExecutionResult>` that awaits its
work; progress and UI updates go to `ctx.Call`, which is the same object the UI binds to.

## Parameter slots

Recognised by the binder:

- `IToolExecutionContext` — once
- `IToolCall` — once; a shorthand for `ctx.Call` when a method only needs the call
- `[OriginalArgs] JsonNode` — once
- `[SharedContext] T` — once
- `CancellationToken` — once
- `[Inject] T` — any number; resolved from `ctx.Services` (not `ctx.Chat.Services`)
- everything else is a JSON argument

`ReactiveToolResult` is **not** a slot anymore.

## Chat-only parts

`ctx.Call.AddAdditionalData(part)` is the only way to attach a UI part. In chat the part is stored on the
call; in a task the call projects only a `NativeAttachmentMessagePart` into its attachments
(`NativeAttachment` → `AgentAttachment.TryConvertFromNativeAttachment`) and silently drops the rest. Do
**not** cast the call to `Domain.ToolCall` to reach `AdditionalData` directly — that breaks the task path.

Attachments produced as data (images, audio, video) use `ctx.Call.AddAttachment(new ToolAttachment(...))`
instead of adding an attachment part by hand.

## Capability methods (preview / streaming / specifier / sync / approval)

- Preview: `(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct) → Task<PreviewToolExecutionResult>`.
  Write status/content into `ctx.Call`; return only the control outcome (`InterruptingSuccess`,
  `ExpectedBehaviour`).
- Streaming analyzer: `(JsonNode? partialArgs, IToolExecutionContext ctx) → StreamingToolAnalysisResult`.
  Status icon/title go into `ctx.Call`.
- Specifier analyzer: `(Specifier specifier, JsonNode? args, IToolExecutionContext ctx) → SpecifierMatchResult`.
- Sync analyzer (new, only where an agent is told to add one): `(JsonNode? args, IToolExecutionContext ctx)
  → IReadOnlyList<ToolSyncKey>`.
- Approval provider (new, only in P6): `AwaitDecisionAsync(ctx, ct) → Task<ToolApprovalResult>`.

## Files in the sweep

- `Tools/Implementations/**` (root modules, Filesystem, Memory, Web)
- `Tools/Implementations/Scripting/**`
- `Tools/Scripting/**` (parser and engine contract)
- `Scripting/Lua/**` (engine + `LuaApiTools` + `LuaApiAgents`), `Scripting/CSX/**`
- `MCP/**`
- `src/LLMDesktopAssistant.Desktop/ToolModules/**`, `Scripting/Python/**`
- task-side tools: `Agents/Tasks/*Tool*.cs`, `Agents/Memory/AgentMemoryTools.cs`,
  `Agents/SubAgents/AgentSubAgentTools.cs`
- tests that construct tools by hand (`tests/**`)

## Report format

For each file: `path — migrated | skipped (reason) | needs attention (why)`. Ambiguities are reported
verbatim; the lead agent decides.
