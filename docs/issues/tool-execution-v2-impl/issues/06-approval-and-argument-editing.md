# P6 — Approval separation and argument editing

Status: open
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

## Acceptance

- Unit tests: confirmation matrix (Approve/Ask/Disallow × `HandledDecisions` × sink/provider),
  memorization, cancellation, effective-arguments precedence.
- `ToolExecutionContext.ConsentContext` is replaced by `ctx.Consent`.
- A file edit shows its diff before execution and executes exactly the previewed change.
