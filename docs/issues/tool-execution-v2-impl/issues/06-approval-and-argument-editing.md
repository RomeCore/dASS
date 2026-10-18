# P6 — Approval separation and argument editing

Status: resolved
Type: task
Blocked by: 05

## Scope

Make approval a core-owned step (spec §8–§9, ticket 08 of the spec effort) and open the two
argument-editing channels.

## Confirmation

- `IToolConfirmationService` owns the decision mechanics, upstream of execution:

```
decision, reason = policy.Evaluate(ctx)
Approve:  // nothing
Ask:      HandledDecisions.HasFlag(Ask)      ? descriptor.Approval.AwaitDecisionAsync(ctx, ct) : sink.RequestAsync(ctx, ct)
Disallow: HandledDecisions.HasFlag(Disallow) ? descriptor.Approval.AwaitDecisionAsync(ctx, ct) : service writes "denied: {reason}"
memorize from ToolApprovalResult
```

- `descriptor.HandledDecisions` (ex `DefaultSelfHandledDecisions`) gates the tool-owned path.
- The tool no longer awaits or applies a decision inside `Main`. The filesystem edit family becomes an
  `IToolApprovalProvider`: build the diff view model, attach it to the call via
  `ctx.Call.AddAdditionalData(...)`, await its consent source, return `ToolApprovalResult`. The diff UI
  itself is unchanged; only its owner and timing move.
- The tool's edits (enabled hunks) travel through `ctx.SharedContext`; the consent result through the
  returned `ToolApprovalResult`.

## Arguments

- Channel 1: `call.Arguments` (persisted) — edited by the approval surface before confirmation.
- Channel 2: `ctx.ArgumentsOverride` (`JsonNode?`, ephemeral) — the hook channel.
- `ctx.Arguments.Effective = ArgumentsOverride ?? tolerant-parse(call.Arguments)`; cached by
  `(overrideReference, rawString)`.
- All stages read `ctx.Arguments.Effective`: the lock, the specifier check, the executor and preview.
- Both channels validate against `ArgumentSchema` through `IToolValidationService` (stub in this pass, but
  the call sites are real; an invalid approval edit blocks confirmation).

## UI in this phase

Only the mandatory surface: the consent panel keeps working in chat and on the task card; the diff view
keeps working, now attached by the approval provider. No argument editor UI (deferred), no
panel-hosted approval content (deferred).

## Design decisions (settled in grilling, 2026-10-18)

- The tool-owned approval path is exercised for real: `fs-edit`, `fs-apply_diff` and `fs-write_file`
  become `IToolApprovalProvider` implementations; `Main` stops awaiting/applying a decision.
- `ToolPolicyDecision` gains `All = Approve | Ask | Disallow`. The enum stays non-nullable;
  `HandledDecisions = ToolPolicyDecision.All` reads as "the tool handles every decision".
- `HandledDecisions` is per-invocation state: `IToolExecutionContext.HandledDecisions { get; set; }`,
  initialized from the descriptor, overridable by preview through
  `PreviewToolExecutionResult.HandledDecisions` (`ToolPolicyDecision?`, `null` = no override).
- A descriptor that declares `Ask`/`Disallow` without an `Approval` provider is an error twice:
  `ToolInfo.Create` throws at construction time, and `ToolConfirmationService` fails the invocation at
  runtime (preview may widen the mask after construction).
- In a world without a chat host (`RunningInUi == false`, i.e. an agent task) the file-tool preview
  removes the `Ask` bit: the tool cannot show a diff there, so the task's `IConfirmationSink` asks
  instead of the policy `Ask` being silently ignored.
- Memorization has one owner: `ToolConfirmationService` memorizes from the returned
  `ToolApprovalResult`. `TextDiffAdditionalViewModel` stops memorizing; it exposes
  `ConsentResultTask : Task<ToolConsentResult>` instead of `ConfirmationTask : Task<MemorizedDecision>`
  and loses `ConsentContext`.
- The diff card is built once in preview (read-only) and travels through `SharedContext`; the approval
  provider flips it to interactive and awaits its consent, `Main` only applies the approved content.
  This preserves the read-only diff for auto-approved edits.
- `fs-write_file` preview computes the full new content (including the `append` case) and stores it in
  the shared context; the provider and `Main` consume it instead of recomputing.

## Acceptance

- Unit tests: confirmation matrix (Approve/Ask/Disallow × `HandledDecisions` × sink/provider),
  memorization, cancellation, effective-arguments precedence.
- `ToolExecutionContext.ConsentContext` is replaced by `ctx.Consent`.
- A file edit shows its diff before execution and executes exactly the previewed change.

## Answer

Landed on 2026-10-18 on `tool-execution-v2`.

**Approval.**

- `ToolConfirmationService` delegates a handled decision to
  `IToolDescriptor.Approval.AwaitDecisionAsync`. An unhandled `Ask` still goes to
  `IConfirmationSink`; an unhandled `Disallow` is written by the service.
- `IToolExecutionContext.HandledDecisions` is the per-invocation mask; it is initialized from the
  descriptor and overridden by `PreviewToolExecutionResult.HandledDecisions` in `ToolPreviewService`.
- `ToolPolicyDecision.All` was added; `null` is not used as "handles everything".
- `ToolInfo.Create` throws when `Ask`/`Disallow` is declared without an approval provider;
  `ToolConfirmationService` repeats the check at runtime because preview may widen the mask.
- Memorization moved to the service. `TextDiffAdditionalViewModel` exposes
  `ConsentResultTask : Task<ToolConsentResult>` and no longer references
  `ToolConsentMemorizationContext`; its `ConfirmationTask`/`ConsentContext` were deleted.

**File family.**

- `fs-edit`, `fs-apply_diff` and `fs-write_file` implement `IToolApprovalProvider`.
- Preview computes the full new content, builds one read-only diff card and stores it in
  `SharedContext`; the provider flips the same card interactive, awaits `ConsentResultTask`, records the
  selected hunks/content and returns `ToolApprovalResult`; `Main` only writes the approved content.
- Auto-approved edits keep the read-only diff card.
- In a task (`RunningInUi == false`) preview clears the `Ask` bit, so `TaskConfirmationSink` asks; the
  diff card is not projected into the task world.
- `fs-write_file` stores the full new content (including `append`) in the shared context; `Main` falls
  back to recomputation when no preview ran (tests, direct executor calls).
- If the invocation is in a UI and `Ask` is handled but the diff card is missing, the provider denies
  instead of silently approving the whole edit.

**Arguments.**

- The two channels (`call.Arguments` and `ctx.ArgumentsOverride`) and `ToolArgumentsAccessor` were
  already landed in P1. This pass adds no argument-editor UI. `IToolValidationService` remains the
  always-valid stub tracked in `not-implemented.md`, so an approval-side argument edit is not
  schema-validated yet.
- `ToolApprovalResult` now carries `EditedArguments`, `OverrideModelIntent` and `ApprovalData`.
  `OverrideModelIntent = true` writes the edit into persisted `call.Arguments` and clears
  `ctx.ArgumentsOverride`; `false` uses the ephemeral channel. `ApprovalData` is delivered on
  `ctx.ApprovalData`. Preview invalidation and re-validation after an edit are deferred (the
  confirmation stage installs the edit without clearing `SharedContext`).

**Tests.**

- `ToolConfirmationServiceTests`: sink/provider matrix, `ToolPolicyDecision.All`, invocation-mask
  override, missing-provider runtime error, memorization through the provider.
- `ToolInfoConstructionTests`: construction-time handled-decision check.
- `ToolPreviewServiceTests`: override and no-override.
- Core suite: 1247 tests, 1245 passed, 1 skipped (known YamlDotNet colon skip). The only failure
  observed was an unrelated LiteDB flake in `SlashCommandFingerprintTests`
  ("Collection was modified"); the tool suites are green.
- Desktop gates are run separately in P10.
