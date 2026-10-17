# P10 — Docs and final gates

Status: open
Type: task
Blocked by: 01, 02, 03, 04, 05, 06, 07, 08, 09

## Scope

Close the effort: make the repository compile, make the tests pass, make the docs true, commit once.

## Documentation

- `AGENTS.md`: replace the tool-execution description with the new core — descriptor/call/context,
  executors, stage services, orchestrators and sources, the trust model. Remove every mention of
  `ToolExecutionService`, `ReactiveToolResult`, `ToolExecutionContext` and the `*ExecutorCreator` family.
- `GLOSSARY.md`: add the settled terms — tool descriptor, tool call, execution context, invocation source,
  stage service, orchestrator, sync key, approval provider, missing-tool descriptor.
- `docs/issues/tool-execution-v2/map.md`: mark the map as closed with a pointer to
  `docs/issues/tool-execution-v2-impl/plan.md`; do not rewrite the spec.
- If a phase diverged from the spec, update the spec section and the ticket answer.

## Gates

```powershell
dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj'
$a = Join-Path $env:TEMP 'dass-temp-build'
dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath="$a"
dotnet build 'tests/LLMDesktopAssistant.Tests/LLMDesktopAssistant.Tests.csproj'
dotnet build 'tests/LLMDesktopAssistant.Desktop.Tests/LLMDesktopAssistant.Desktop.Tests.csproj'
dotnet test  'tests/LLMDesktopAssistant.Tests/LLMDesktopAssistant.Tests.csproj'
dotnet test  'tests/LLMDesktopAssistant.Desktop.Tests/LLMDesktopAssistant.Desktop.Tests.csproj'
```

## Manual smoke

Run the nine scenarios listed in [`../plan.md`](../plan.md#manual-smoke-p10).

## Commit

One commit on `tool-execution-v2`, compiling, with the tickets resolved and `not-implemented.md` accurate.
No other uncommitted leftovers.
