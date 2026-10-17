# P5 — Agent-task path

Status: resolved
Type: task
Blocked by: 04 (complete)

## Scope

Re-express the task tool path as `AgentTaskToolOrchestrator` (Application-scoped) over the shared
services (spec §16, ticket 05 of the spec effort).

## What changes

- Delete the duplicated `preview → policy → specifier → approval → memorization → consent → execution`
  block inside `AgentTaskExecutor.ExecuteToolAsync`.
- `AgentTaskExecutor` keeps: message plumbing (`AgentAssistantMessage` / `AgentToolCall` /
  `AgentAttachment` and the lossy projections), `MaxParallelToolCalls`, chat-trigger wiring,
  `OnlyResponse`, and the loop that turns tool results into `ToolMessage`s.
- `AgentTaskToolOrchestrator` runs `validation → sync{ preview → policy → confirmation (implicit) →
  execution } → settle` with `Source = AgentTask`.
- `TaskConfirmationSink` drives `AgentTask.ToolCallConfirmationRequests` and the task card.
- `ChatAgentTool` **dissolves**: the task toolset becomes `ImmutableDictionary<string, IToolDescriptor>`
  (or a list) and receives a chat `ToolInfo` directly. A small projection helper (`Clone → stamp
  TaskToolContextFactory → Freeze`) is used by `AgenticToolModule`, `SubAgentToolResolver` and
  `LuaApiAgents`.
- `DelegateAgentTool` uses the shared `DelegateMainExecutor`.
- `MemorizeAlwaysInToolset` moves into the confirmation stage / chat-scoped sink path, not the task
  executor.

## Decisions settled while implementing (2026-10-18)

1. **Tool collections are neutral.** `AgentTaskLaunchParameters.Tools` and
   `DirectTaskSubAgentDescriptor.Tools` become `ImmutableList<IToolDescriptor>`; a chat `ToolInfo` goes in
   unchanged. `AgentTool` stays the base for task-native tools (`SkillLoadTool`, the memory/sub-agent
   delegate tools); it already implements `IToolDescriptor` through its carried executors.
2. **The projection helper clones only when it must.** A chat `ToolInfo` is used as is; a per-task
   approval override (sub-agent *allowed* → `AlwaysApprove`, *disallowed* → `AlwaysDisallow`) needs a
   `Clone → mutate → Freeze`. The clone clears the chat-stamped `ContextFactory`, because the task entry
   passes `TaskToolContextFactory` in the request and the dispatcher uses it verbatim.
3. **`TaskConfirmationSink` owns the task card *and* its memorization.** It creates the
   `AgentToolCallConfirmationRequest`, awaits the card, maps `ToolConsentResult` to `ToolApprovalResult`,
   and performs `MemorizeConsent` plus the "always" persist. `AgentTaskExecutionContext.Consent` stays
   `null`, so the shared confirmation stage does not memorize a second time.
4. **The argument drain stays at the entry point.** The task sequence has no streaming stage; the
   executor keeps awaiting `PartialFunctionToolCall` and mirrors `Args` into `AgentToolCall.Arguments`
   before dispatching. `AgentTool.Streaming` is `null`, so a stage would only drain anyway.
5. **A missing tool is uniform.** The request passes `Descriptor = null` and the dispatcher substitutes
   `MissingToolDescriptor`; the model sees `Error: Tool '<name>' not found.` instead of today's
   `Tool '<name>' not found.` (`MissingToolDescriptor` owns the wording).
6. **Notes travel as a content part.** The shared confirmation stage appends
   `Additional notes from user: …` as an invisible content part; the task call projects it to an
   `AgentContentAttachment`. The executor folds content attachments into the result content (so the model
   sees them) and passes every attachment to `AgentToolCallResult.Attachments` (native ones become
   `ToolMessage` attachments). Wording differs from today's `User has provided additional notes:`.
7. **`DelegateAgentTool` is not rewritten.** Its execution already runs through `AgentTool.Main`, a
   `DelegateMainExecutor`; the spec's "becomes an instance of `DelegateMainExecutor`" is satisfied at the
   executor level, and the class remains the descriptor.
8. **The executor owns the task factory.** It injects the root `IServiceProvider` and constructs
   `TaskToolContextFactory(TriggeredChat, TriggeredChat?.Services ?? root)` per task.
9. `RunningInUi = false`; `Host = AgentAssistantMessage` (it is an `IToolCallHost`);
   `EffectiveBehaviours` come from the launch parameters.
10. **The model-facing content of a call is built in one helper.** `ToolCallContentBuilder.Build(resultContent,
    payloads)` in `Tools/Execution/Support/` is used by both the chat prompt
    (`AgentPromptComposer`) and the task result projection (`AgentTaskExecutor`). The helper filters the
    `IContentChatAttachment` branch itself, because the two worlds store payloads differently (`AdditionalData`
    vs `Attachments`). The chat separator stays `\n\n` (it is already in every emitted prompt, and SCM
    byte stability forbids changing it); `ToolCallContentProjector` is deleted.

## Acceptance

- `ChatAgentTool` is gone; no wrapper exists between a chat tool and a task.
- An agent task using a chat tool produces the same user-visible behaviour as today (result on the task
  card, confirmation on the card, attachments projected).
- `OnlyResponse` still skips execution entirely.
- `MaxParallelToolCalls` still bounds parallelism.

## Answer

The task path now runs through `AgentTaskToolOrchestrator`; the duplicated block is gone.

- `ChatAgentTool` deleted. `AgentTaskLaunchParameters.Tools` and `DirectTaskSubAgentDescriptor.Tools`
  are `ImmutableList<IToolDescriptor>`. `TaskToolProjection` clones a chat `ToolInfo` only for a per-task
  approval override and clears the chat-stamped context factory.
- `TaskConfirmationSink` (`Tools/Execution/Sinks/`) owns the task-card request **and** the task
  memorization (`MemorizeConsent` + the persistent "always" decision). `AgentTaskExecutionContext.Consent`
  stays `null`, so the shared confirmation stage does not memorize twice.
- `AgentTaskExecutor` keeps the argument drain, `OnlyResponse`, `MaxParallelToolCalls`, the message loop
  and the `AgentToolCall` → `AgentToolCallResult`/`ToolMessage` projection. It dispatches every call with
  `Source = AgentTask`, a per-task `TaskToolContextFactory(TriggeredChat, chat?.Services ?? root)` and
  `RunningInUi = false`.
- A missing tool goes through `MissingToolDescriptor` (uniform pipeline).
- `ToolCallContentBuilder` (`Tools/Execution/Support/`) is the single builder of a call's model-facing
  content, shared by `AgentPromptComposer` and the task projection; `ToolCallContentProjector` is deleted.
  The separator stays `\n\n` (SCM byte stability).
- `SubAgentToolResolver`, `AgenticToolModule`, `LuaApiAgents` and the automatic memory tasks speak
  `IToolDescriptor` instead of `AgentTool`.
- The task card resolves a tool's display name UI-side (`ToolInfo.NameKey` / `AgentTool.DisplayName`); the
  neutral `IToolDescriptor` contract did not grow.

Gates: core build 0 errors; `LLMDesktopAssistant.Tests` 1233 total, 1232 passed, 1 skipped, 0 failed;
Desktop build 0 errors (pre-existing NuGet advisories only). **`Desktop.Tests` were not re-run in this pass**
(the run was cancelled) — run them before the manual smoke.

Deviations from the ticket text: the task sequence has no streaming stage (the drain is entry-point
plumbing); a missing tool reports `Error: Tool '<name>' not found.`; confirmation notes are appended with
the `Additional notes from user:` wording joined by `\n\n` rather than today's `User has provided
additional notes:`.

Manual smoke still pending: a task using a chat tool (result on the card, confirmation on the card,
attachments projected).
