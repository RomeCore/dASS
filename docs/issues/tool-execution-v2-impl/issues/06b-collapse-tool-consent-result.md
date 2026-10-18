# P6b — Collapse `ToolConsentResult` into `ToolApprovalResult`

Status: resolved
Type: task
Blocked by: 06

## Question

After P6 the pipeline carries two consent DTOs:

- `ToolConsentResult` (`Tools/Consents/`): `IsApproved`, `Memorization`, `Notes` — the payload of the UI
  confirmation channel (`IToolCall.UserConfirmationSource`), the input of `IToolMemorizationService`, and
  the result of `TextDiffAdditionalViewModel.ConsentResultTask`.
- `ToolApprovalResult` (`Tools/Execution/Domain/`): `Approved`, `Memorization`, `Notes`,
  `EditedArguments`, `OverrideModelIntent`, `ApprovalData` — what `IConfirmationSink`,
  `IToolApprovalProvider` and the confirmation stage speak.

The first is a strict subset of the second. The decision therefore crosses the boundary twice in the
same direction: the UI produces a consent, the sink/provider copies it into an approval, and the
confirmation stage copies it back into a consent for memorization. Three copies of one triple.

It is also a blocker: `not-implemented.md` records that approval-time argument editing has no producer
exactly because `ToolConsentResult` cannot carry the three editing fields, and P8's restart-with-edited-
arguments is the planned producer.

Should there be one decision type, and which one?

## Decisions

- **One type: `ToolApprovalResult`.** `ToolConsentResult` is deleted. The UI channel, the memorization
  service, the diff card and both sinks speak `ToolApprovalResult`; `Approved` replaces `IsApproved` at
  every consumer.
- **`ToolApprovalMemorization` stays in `Tools.Consents`** (maintainer decision). `Tools.Consents` takes
  a namespace reference to `Tools.Execution.Domain`; both live in the same assembly, so the mutual
  namespace reference is accepted. No enum move in this pass.
- The copies disappear: `ChatConfirmationSink` and `TaskConfirmationSink` return the awaited result as
  is; `ToolConfirmationService.Memorize` passes the same object to `ctx.Consent`;
  `FileSystemEditBaseToolModule.AwaitDiffConsentAsync` records the shared-context state and returns the
  awaited approval unchanged.
- `TextDiffAdditionalViewModel.ConsentResultTask` becomes
  `Task<ToolApprovalResult>`; its `CreateConsent` factory becomes `CreateApproval` and fills `Approved`.
- The stale doc comment on `ToolCall.UserConfirmationSource` (it still describes a null/empty/string
  result) is replaced with the current contract.
- No behaviour change: this is a mechanical type collapse. Tests keep their coverage, only the types and
  the property name change.
- Documentation that states the current contract is updated in the same pass:
  `docs/issues/tool-execution-v2/spec.md` (`IToolCall` snippet),
  `docs/issues/tool-execution-v2-impl/plan.md` (deviation 22) and `not-implemented.md` (the
  approval-time editing seam now names `ToolApprovalResult`). Resolved tickets stay untouched as a
  historical record.

## Answer

Landed on 2026-10-18 on `tool-execution-v2`.

- `src/LLMDesktopAssistant/Tools/Consents/ToolConsentResult.cs` is deleted. `ToolApprovalResult` is the
  single decision payload everywhere: `IToolCall.UserConfirmationSource` (and the `ToolCall`,
  `ScriptToolCall`, `AgentToolCall`, `AgentToolCallConfirmationRequest` implementations),
  `ToolConsentPanel`, `ToolCallViewModel`, `AgentToolCallBriefViewModel`,
  `TextDiffAdditionalViewModel.ConsentResultTask` (`CreateConsent` became `CreateApproval`), both
  confirmation sinks and `IToolMemorizationService` / `ToolMemorizationService` /
  `ToolConsentMemorizationContext`.
- `IsApproved` is gone; consumers read `Approved`.
- The copies are gone: `ChatConfirmationSink` and `TaskConfirmationSink` return the awaited result as is;
  `ToolConfirmationService.Memorize` passes the same object to `ctx.Consent`;
  `FileSystemEditBaseToolModule.AwaitDiffConsentAsync` records the shared-context state and returns the
  awaited approval unchanged.
- `ToolApprovalMemorization` stays in `Tools.Consents` as decided. `Tools.Consents` now references
  `Tools.Execution.Domain`; the mutual namespace reference is accepted inside the single assembly.
- The stale `ToolCall.UserConfirmationSource` doc comment and the stale `ConsentContext` mention in
  `TextDiffAdditionalViewModel.ResolveConsent` were replaced with the current contract.
- Tests updated to the new type and property name: `ToolConfirmationServiceTests`,
  `ToolPolicyServiceTests`, `ToolExecutionContractTests`. No test behaviour changed.
- Docs that state the current contract updated: the `IToolCall` snippet in
  `docs/issues/tool-execution-v2/spec.md`, deviation 22 plus a new deviation 26 in `plan.md`, and the
  approval-time editing seam in `not-implemented.md`. Resolved tickets 05/06 stay untouched as a
  historical record.
- Gates: core build green; core test suite 1250 total, 1249 passed, 1 skipped, 0 failed (the known
  LiteDB flake in `SlashCommandFingerprintTests` did not reproduce). The Desktop build was skipped on
  the maintainer's request.

## Comments
