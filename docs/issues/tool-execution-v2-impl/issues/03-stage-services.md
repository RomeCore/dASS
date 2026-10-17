# P3 — Stage services

Status: resolved
Type: task
Blocked by: 02

## Scope

Extract the eight stage services (spec §4, Appendix A.5) from `ToolExecutionService` into
`Tools/Execution/Services/` (`LLMDesktopAssistant.Tools.Execution.Services`). All are Application-scoped,
stateless, and take their state from `IToolExecutionContext`.

| Service | Responsibility |
|---|---|
| `IToolStreamingService` | drain the argument stream, run the optional analyzer, write `call.Arguments` and status |
| `IToolValidationService` | tolerant-parse + schema validate (stub: always valid) → `ctx.Arguments.Effective` |
| `IToolSynchronizationService` | keyed higher-order scope around the inner steps |
| `IToolPreviewService` | run `descriptor.Preview`, fold the control outcome into the call |
| `IToolPolicyService` | merge `PolicyMask` → specifiers → memorization → approver → decision + reason |
| `IToolConfirmationService` | explicit/implicit confirmation + memorization + "always" persistence (P6 completes it) |
| `IToolExecutionService` | run `descriptor.Main` |
| `IToolSettlementService` | finalize the call; always runs in the caller's `finally` |

## Decisions

- `IToolExecutionContext` gains `IConfirmationSink Confirmation { get; }`. The sink is per-source state
  carried by `ToolContextCreationParams` (required) and cannot be resolved from DI: the confirmation
  service is Application-scoped and stateless. Both concrete contexts store `parameters.Confirmation`.
- User notes are not a context member. The confirmation service writes an
  `AdditionalMessageContentPart` (`IsVisible = false`, `IsRestorable = false`,
  `Content = "Additional notes from user: …"`) onto the call through the existing write path;
  `AgentPromptComposer` appends the call's content parts to the tool result, skipped when the call is
  compacted. Settlement does not touch notes.
- The agent's auto-approve/disallow policy is computed at context creation and travels in
  `ToolContextCreationParams.EffectiveBehaviours`; `IToolPolicyService` reads only
  `ctx.EffectiveBehaviours`, the descriptor and memorization, so it stays chat-free. The transitional
  sequencer passes `descriptor.ContextFactory.CreateContext(...)` (falling back to a locally built
  `ChatToolContextFactory` for `MissingToolDescriptor`), which removes the duplicated consent wiring.
- The preview stage owns `call.ExpectedBehaviour`: it writes `descriptor.DefaultExpectedBehaviour`
  first and lets the preview result override it. The policy stage never needs the concrete call's
  nullable behaviour.
- `IToolSynchronizationService.RunAsync` takes a `CancellationToken`: waiting for the key set is
  cancellable, and the run token is the only token available until P8 links the per-call source.
- Key kinds are `File`, `Directory`, `Custom` and the new `AllFiles`. `AllFiles` conflicts with every
  `File`, `Directory` and `AllFiles` key (not with `Custom`) and its value normalizes to empty. There is
  no magic empty `Directory`.
- Path keys compare OS-aware (`OrdinalIgnoreCase` on Windows, `Ordinal` otherwise); `Custom` compares
  `Ordinal`. Canonicalization trims trailing separators (roots preserved), dedupes, drops empty path
  values and collapses a `File`/`Directory` covered by a requested `Directory`.
- The filesystem analyzer covers the three mutating edit tools (`fs-edit`, `fs-write_file`,
  `fs-apply_diff`). Their family-wide `Custom "fs-edit"` static key is removed; the analyzer returns
  that key as the fallback when the `path` argument is missing or cannot be resolved.
- The chat-facing `IToolExecutionService` / `ToolExecutionService` are renamed
  `IChatToolExecutionService` / `ChatToolExecutionService`; the stage interface keeps the spec name.

## Synchronization

`IToolSynchronizationService.RunAsync(ctx, inner, ct)`:

- keys = `descriptor.StaticSyncKeys` + `descriptor.SyncAnalyzer?.AnalyzeSyncKeys(...)`.
- typed keys (`File` / `Directory` / `Custom` / `AllFiles`), hierarchical prefix conflicts
  (a held `Directory` conflicts with any `File` beneath it, `AllFiles` with any file key).
- canonicalization only (normalize, collapse a file under an already-requested directory); the analyzer is
  the one that absolutizes paths.
- atomic acquisition of the whole key set — no hold-and-wait, no deadlock; release in `finally`.
- one Application-scoped holder; the per-chat `SemaphoreSlim` dictionary disappears with the old service.

## Filesystem analyzer

`FilesystemSynchronizationAnalyzer` derives a `File` key from the `path` argument (absolutized through
`IWorkingDirectoryAccessService`, resolved from `ctx.Services`; access denial is irrelevant — the key is
about the path, not about permission). `fs-edit`, `fs-write_file` and `fs-apply_diff` use it. This is the
proof of the hierarchical model; other tools keep static keys.

## Policy and confirmation

- `IToolApprovalService.ApproveTool` is consumed by `IToolPolicyService`; agent policy arrives through
  `ctx.EffectiveBehaviours`.
- `ToolPolicyReason` (`ToolDecisionSource` + message) is written onto the context by policy and read by
  confirmation.
- Confirmation keeps today's semantics: an unhandled `Disallow` writes the denial into the call and
  stops; an unhandled `Ask` awaits `ctx.Confirmation`, memorizes through `ctx.Consent` and writes the
  cancellation into the call when rejected. `HandledDecisions` plumbing (the descriptor's own approval
  provider) lands in P6.
- `ToolConsentPanel` stays the shell. `IConfirmationSink` is passed by the caller (P4/P5).

## Acceptance

- Unit tests: synchronization (atomic multi-key, prefix conflict, canonicalization, concurrent
  no-deadlock, cancellation while waiting, `AllFiles`), policy (mask merge, specifier combine,
  memorization precedence), confirmation (deny, ask/cancel, notes part), streaming (cyclic/once),
  settlement, validation.
- `ChatToolExecutionService` still exists (it is rewritten as an orchestrator in P4), but its stages are
  gone: it only sequences the services.
- The core and Desktop projects build; both test projects pass.

## Answer

### What landed

`Tools/Execution/Services/` holds the eight Application-scoped services (`IToolStreamingService`,
`IToolValidationService`, `IToolSynchronizationService`, `IToolPreviewService`, `IToolPolicyService`,
`IToolConfirmationService`, `IToolExecutionService`, `IToolSettlementService`), each as a stateless
`[Service]` singleton. The transitional chat path is renamed to
`IChatToolExecutionService`/`ChatToolExecutionService` and now only assembles the context and sequences
the stages; the per-chat `SemaphoreSlim` dictionary, the inline preview/policy/consent/settlement code and
the duplicated consent wiring are gone.

- The context gained `IConfirmationSink Confirmation`; both concrete contexts store it. The chat
  sequencer builds the context through `descriptor.ContextFactory` with a locally built
  `ChatToolContextFactory` fallback for `MissingToolDescriptor`, and passes the agent's
  `AutoApproveBehaviours`/`DisallowedBehaviours` as `ToolContextCreationParams.EffectiveBehaviours`.
- The preview stage owns `ExpectedBehaviour`: it writes the descriptor default and lets the preview
  result override it, so the policy stage reads only the neutral call.
- Confirmation keeps the pre-P6 semantics and writes the user's notes as an invisible
  `AdditionalMessageContentPart`. `AgentPromptComposer` appends the call's content parts to the tool
  result, skipped when the call is compacted. `not-implemented.md` no longer lists the projection.
- `ToolSyncKeyKind.AllFiles` was added; path keys compare OS-aware and canonicalization trims trailing
  separators, dedupes, drops empty values and collapses keys under a requested directory. Acquisition is
  all-or-nothing under one mutex with a broadcast release signal, so there is no hold-and-wait and no
  lost wake-up.
- `FilesystemSynchronizationAnalyzer` derives a `File` key from the `path` argument (absolutized via
  `IWorkingDirectoryAccessService` from `ctx.Services`) and falls back to `Custom "fs-edit"` when the
  path is missing or unusable. It is wired on `fs-edit`, `fs-write_file` and `fs-apply_diff`; their
  static `Custom "fs-edit"` key was removed.
- `IToolSynchronizationService.RunAsync` takes a `CancellationToken`; the spec and Appendix A.5 were
  updated in the same pass.

### Corrections to the planned API

- `IToolPolicyService` does not resolve the agent or the chat settings; the invocation's behaviours
  arrive on the context. This keeps the service chat-free and unit-testable.
- `ToolSynchronizationService.Canonicalize` is a public pure static helper: the tests assert the
  normalization/collapse rules directly, and the service remains its only production caller.

### Tests

`tests/LLMDesktopAssistant.Tests/Tools/Execution/` covers the keyed scope (serialization, concurrency,
prefix conflict, `AllFiles`, overlapping key sets, cancellation while waiting, canonicalization), the
policy stage (approval level, mask merge, memorized denial and approval), the confirmation stage (deny,
ask/cancel, notes part, handled decision), streaming (`Once`/`Cyclic`, analyzer without a stream),
settlement and validation. `ToolTestContext` gained optional behaviours, a confirmation sink, an
argument stream, extra services and a minimal descriptor builder.

### Gates

- Core project: builds, 0 errors, 0 warnings.
- Desktop project: builds, 0 errors (only the pre-existing NuGet advisory warnings).
- `LLMDesktopAssistant.Tests`: 1202 total, 1201 passed, 1 skipped (the YamlDotNet colon case), 0 failed.
- `LLMDesktopAssistant.Desktop.Tests`: 44 total, 44 passed, 0 failed.
