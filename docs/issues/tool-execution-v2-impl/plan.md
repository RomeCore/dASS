# Tool execution v2 — implementation

Status: in progress
Spec (authority): [`../tool-execution-v2/spec.md`](../tool-execution-v2/spec.md)
Tickets: [`issues/`](issues/) — one per phase, written before its code.
Deferred work: [`not-implemented.md`](not-implemented.md)
Rules for the tool sweep: [`tools-migration-rules.md`](tools-migration-rules.md)

Branch: `tool-execution-v2`. One commit at the end; the tree must compile at the commit.
Between phases the tree may be red — that is expected and accepted.

## Rules of the effort

- **Ticket first, code second.** A ticket records what is about to be built; when the phase is done it is
  marked `resolved` with an `## Answer` that says what actually landed and what diverged from the spec.
- **The spec is the starting point.** If a phase discovers a better shape, the spec and the ticket change
  in the same pass; the code is never bent to a stale document.
- **No compatibility shims.** Old types are deleted in the phase that replaces them. A temporary second
  execution path costs more than a mechanical sweep.
- **Namespace matches the folder.** New subsystem lives under `Tools/Execution/`.
- **DTOs are plain classes with init-only properties**, not records. Value types (`ToolSyncKey`,
  `ToolPolicyReason`) stay `readonly record struct` — they exist for value equality.
- **`IToolDescriptor?` at the invocation boundary.** A call whose tool does not exist must still be
  processed: the dispatcher substitutes an internal `MissingToolDescriptor` and the pipeline stays
  uniform; `IToolExecutionContext.Descriptor` is never null.
- **~70 tool methods live in far fewer files.** The sweep is partitioned **by file**, and the rulebook is
  written **by pattern** (sync tool, async tool, prepared-result tool, preview/specifier tool, UI part).
- **Chat orchestrators are Chat-scoped**; `AgentTaskToolOrchestrator` is Application-scoped, because an
  agent task is an application service that only attaches to a chat for dispatching.
- **Desktop is a first-class gate.** The Desktop tool modules, the Python engine and the Desktop test
  project are migrated in the same phase as the core ones.

## Phases

| # | Ticket | Outcome | Depends on |
|---|---|---|---|
| P1 | [Domain contracts](issues/01-domain-contracts.md) | The neutral contracts, concrete chat/task contexts, factories, missing-tool descriptor, argument accessor, sugar extensions. Old execution path still alive. | — |
| P2 | [Binder and the tool sweep](issues/02-binder-and-tool-sweep.md) | `ToolMethodBinder` + `DelegateMainExecutor`; `ToolInfo`/`AgentTool` carry executors; `ToolExecutionContext` → `IToolExecutionContext`; `ReactiveToolResult` deleted; all tool files, script engines, MCP and Desktop modules migrated by a sub-agent swarm. | P1 |
| P3 | [Stage services](issues/03-stage-services.md) | The eight Application-scoped stage services extracted from `ToolExecutionService`; typed-key synchronization with prefix conflicts and a filesystem analyzer. | P2 |
| P3.5a | [Attachments model](issues/03a-attachments-model.md) | One write path (`IChatAttachment` with native and content branches); `ToolAttachment`, `ToolAttachmentKind`, `AgentAttachmentType` and `AddAdditionalData` deleted; `AgentAttachment` split. | P3 |
| P3.5b | [Remove CompletionToken](issues/03b-remove-completion-token.md) | `CompletionToken` leaves `ToolCall` and `AssistantMessage`; completion is the terminal status. | P3 |
| P4 | [Orchestrators and dispatcher](issues/04-orchestrators-and-dispatcher.md) | Chat-scoped dispatcher, `ChatToolOrchestrator`, `ChatRestartOrchestrator`, `ToolCommandOrchestrator`, `ScriptToolOrchestrator`; `ChatExecutionService` rewired; `IToolExecutionService` deleted. | P3.5b |
| P5 | [Agent-task path](issues/05-agent-task-path.md) | `AgentTaskToolOrchestrator`; the duplicated block in `AgentTaskExecutor` deleted; `ChatAgentTool` dissolved; `TaskConfirmationSink`; `MaxParallelToolCalls`; `OnlyResponse`. | P4 |
| P6 | [Approval separation and argument editing](issues/06-approval-and-argument-editing.md) | Core-owned confirmation step, `IToolApprovalProvider`, `HandledDecisions`, `ToolPolicyReason`; file tools stop approving inside execution; two argument channels and the effective-arguments accessor. | P5 |
| P6a | [Default approval level](issues/06a-default-approval-level-resolution.md) | `ApplyDefaults` hook in the addon collector; the toolset default reaches every tool, not only the ones with a `ToolChange`; `AvailableTools` stays default-free. | P5 |
| P7 | [Cascade approval](issues/07-cascade-approval.md) | Resolution cascade and memory reach over the sibling calls; duration from the existing memorization options. Reach picker deferred. | P6 |
| P8 | [Restart and cancel](issues/08-restart-and-cancel.md) | Per-call CTS, `ChatRestartOrchestrator` in use, inline cancel/restart buttons on the tool call, consent cancellation, output preservation. | P6 |
| P9 | [Tool-command source](issues/09-tool-command-source.md) | `DerivedSlashCommandProvider<ToolInfo>`, argument schema generation (designated main argument → positional/rest), run through the `Command` orchestrator. | P4 |
| P10 | [Docs and final gates](issues/10-docs-and-final-gates.md) | `AGENTS.md`, `GLOSSARY.md`, full build and test gates, manual smoke list, the single commit. | P1–P9 |

## Gates

At the end of the effort (P10), and as soon as a phase makes them reachable:

```powershell
dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj'
$a = Join-Path $env:TEMP 'dass-temp-build'
dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath="$a"
dotnet build 'tests/LLMDesktopAssistant.Tests/LLMDesktopAssistant.Tests.csproj'
dotnet build 'tests/LLMDesktopAssistant.Desktop.Tests/LLMDesktopAssistant.Desktop.Tests.csproj'
```

Android / Browser / Blazor are not dedicated gates; they are checked only if a shared change breaks them.

## Manual smoke (P10)

1. A chat turn with a math tool (no approval) and a filesystem tool (approval) — both succeed.
2. Approval with `Always` persists; a second call is not asked.
3. File edit with a diff preview: the diff appears before execution, approval executes the previewed change.
4. Cancel one running tool: the run continues, the call shows cancelled, output is kept.
5. Restart a completed tool from the flyout: the call is re-run in place, the next model turn sees the new result.
6. An agent task using a chat tool: result lands on the task card, confirmation appears on the card.
7. A Lua tool calling another tool: the call runs through the script source (no policy/confirmation).
8. `/tool:<name>` runs the tool as a user action, bypassing approval but honouring `AlwaysDisallow`.
9. A tool that does not exist: the call settles with `Tool '<name>' not found`, the run continues.

## Deviations from the spec (recorded here, reflected in the tickets)

1. `IToolDescriptor` is optional in `ToolInvocationRequest`; `MissingToolDescriptor` keeps the pipeline
   uniform and `ctx.Descriptor` non-null.
2. `IToolCall.CancellationTokenSource` is `{ get; set; }` (reactive on the chat call), not get-only.
3. `IToolCall.AddAdditionalData(AdditionalChatData)` is part of the neutral contract — an *input*, not
   state. The chat call stores it; the task call projects only a `NativeAttachmentMessagePart` into its
   attachments and drops everything else. The rule "chat richness stays on the concrete call" therefore
   governs stored state, not this projection payload.
4. Cascade reach has no UI in this pass; the core implements reach, the picker is deferred.
5. The file tools' diff UI is attached by their own approval provider; `ToolConsentPanel` stays the
   generic shell. Panel-hosted custom approval content is deferred.
6. `ToolContextCreationParams.Host` is `IToolCallHost?`, not the spec's `ChatMessage?`: the task's
   assistant message is not a `ChatMessage`, and the core only needs the sibling calls.
7. `IToolExecutionContext` carries `IConfirmationSink Confirmation`: the sink is per-source state and a
   stateless Application-scoped service cannot resolve it.
8. User consent notes travel as an invisible `AdditionalMessageContentPart` on the call, not as a
   context member; `AgentPromptComposer` appends content parts to the tool result.
9. `IToolSynchronizationService.RunAsync` takes a `CancellationToken`; key kinds gain `AllFiles`.
10. The attachments model replaces `ToolAttachment`/`ToolAttachmentKind` and folds
    `IToolCall.AddAdditionalData` into `AddAttachment(IChatAttachment)` (P3.5a).
11. `CompletionToken` is removed from `ToolCall` and `AssistantMessage`; completion is the terminal
    status (P3.5b).
12. `ToolInvocationRequest` carries `ContextFactory`; the dispatcher is **Application-scoped and
    stateless**, and routes all five sources. The entry point resolves the factory (the descriptor's
    stamped one for chat tools, `ScriptToolContextFactory` for scripts, `TaskToolContextFactory` for
    tasks in P5). The chat-scoped variant in the P4 plan is gone (P4).
13. `IToolSettlementService.Finalize(ctx, Exception? exception)` — the parameter has no default. The
    orchestrators have no `finally`; they settle at the end of the `try` or in `catch`, and settlement
    classifies cancellation vs error (P4).
14. `IToolValidationService.Validate` throws `ToolArgumentsInvalidException` instead of returning a
    `bool`; one error channel for the pipeline (P4).
15. `Script` skips the synchronization stage; key reentrancy is deferred (P4, see
    `not-implemented.md`).
16. The script world has its own context: `ChatScriptToolExecutionContext` + `ScriptToolContextFactory`
    (no host message, no consent). `ChatToolContextFactory` is not branched (P4).
17. User actions (`ChatRestart`, `Command`) do not check the hard floor; spec §13 was updated (P4).
18. `ToolPolicyDecision` gains `All = Approve | Ask | Disallow` for the handled-decisions mask;
    the enum stays non-nullable. `HandledDecisions` is per-invocation
    (`IToolExecutionContext.HandledDecisions`, initialized from the descriptor), and preview overrides
    it through `PreviewToolExecutionResult.HandledDecisions` (`null` = keep the descriptor mask).
19. A descriptor that declares `Ask`/`Disallow` without an `Approval` provider throws in
    `ToolInfo.Create`. `ToolConfirmationService` repeats the check at runtime because preview may widen
    the mask after construction.
20. The filesystem family (`fs-edit`, `fs-apply_diff`, `fs-write_file`) is the first real
    `IToolApprovalProvider`: preview builds one read-only `TextDiffAdditionalViewModel` and stores it in
    `SharedContext`; the provider flips the same card interactive and awaits; `Main` only applies the
    approved content. The read-only card is preserved for auto-approved edits.
21. In a task (`RunningInUi == false`) the file-tool preview clears the `Ask` bit, so
    `TaskConfirmationSink` asks instead of the policy `Ask` being silently ignored. The diff card is
    not projected into the task world.
22. Memorization has one owner: `ToolConfirmationService` memorizes from `ToolApprovalResult`.
    `TextDiffAdditionalViewModel` exposes `ConsentResultTask : Task<ToolConsentResult>`; its
    `ConsentContext` and `ConfirmationTask : Task<MemorizedDecision>` are gone.
23. `AddonSetCollectorBase` gains `ApplyDefaults(target, setConfig, agent)`, called for every included
    clone in both branches of `GetAddonsWithChanges`. `ToolsetCollector` uses it to stamp
    `ContextFactory` and resolve `ApprovalLevel = tool ?? toolset default ?? PolicyBased`; the toolset
    default now reaches untouched tools. `AvailableTools` stays default-free (P6a).
24. The `fs-write_file` preview computes the full new content (including `append`) into the shared
    context; `Main` no longer re-applies `append` when the context is present and falls back to
    recomputation for direct executor calls and tests.
25. `ToolApprovalResult` carries `EditedArguments`, `OverrideModelIntent` and `ApprovalData`.
    `OverrideModelIntent = true` installs the edit into the persisted `call.Arguments` and clears
    `ctx.ArgumentsOverride`; `false` installs it into the ephemeral `ctx.ArgumentsOverride`.
    `ApprovalData` is delivered on `ctx.ApprovalData` and read by the tool. Re-validation and
    `SharedContext` re-preview after an edit are deferred (see `not-implemented.md`); the confirmation
    stage installs the edit without clearing the preview artifact.
