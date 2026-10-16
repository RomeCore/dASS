# Separating approval from execution

Status: resolved
Type: grilling
Blocked by: 02

## Question

Today the file-editing tools carry the approval inside their own execution: they show their confirmation interface and then do the work in one step. This ticket settles how approval becomes a step of its own, upstream of execution; how those tools stop handling approval themselves; and what contract lets a tool still contribute a custom approval interface (the diff-based one) into the approval step without owning that step. This is feature goal two, and it is the hinge that argument editing, the approval helper, and cascade approval all hang off.

## Answer

Approval becomes a **core-owned step** (`IToolConfirmationService`), upstream of execution, and the tool **stops owning it** — it no longer awaits a user decision or applies it inside its executor. (Today the file-editing tools do exactly that: `DefaultSelfHandledDecisions = Approve | Ask`, an editable `TextDiffAdditionalViewModel` built and awaited inside the main executor.)

The tool contributes its custom approval interface through a **dedicated capability on the descriptor** (the sixth executor). There is no separate surface object — the provider *is* the await, and its result carries the memorization:

```csharp
public interface IToolApprovalProvider                                          // 6th executor
{
    JsonObject? RequestedDataSchema { get; }                                    // schema of the `requested_data` the helper/panel fills (e.g. enabled hunks), or null
    Task<ToolApprovalResult> AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct);
}
public sealed class ToolApprovalResult
{
    public required bool                     Approved      { get; init; }
    public required ToolApprovalMemorization Memorization  { get; init; }
    public          string?                  Notes         { get; init; }
}
```

The confirmation service owns the mechanics, gated by an optional filter mask on the descriptor (`HandledDecisions : ToolPolicyDecision` — ex-`DefaultSelfHandledDecisions`):

```
decision, reason = policy.Evaluate(ctx)
switch (decision)
{
    Approve:  // nothing
    Ask:      HandledDecisions.HasFlag(Ask)      ? await descriptor.Approval.AwaitDecisionAsync(ctx, ct) : default consent panel
    Disallow: HandledDecisions.HasFlag(Disallow) ? await descriptor.Approval.AwaitDecisionAsync(ctx, ct) : service writes "denied: {reason}" into the result
}
memorize; settle
```

A tool may handle only `Ask` and leave the `Disallow` mechanics to the service. Because a tool that *does* handle `Disallow` needs the reason, `ctx` carries both the decision and its source:

```csharp
enum ToolDecisionSource { ApprovalLevel, PolicyMask, Specifier, Memorization, User }
readonly record struct ToolPolicyReason(ToolDecisionSource Source, string? Message);
// ctx.PolicyDecision : ToolPolicyDecision ; ctx.PolicyReason : ToolPolicyReason?
// the confirmation service memorizes from ToolApprovalResult (which carries Memorization)
```

### Approval outputs flow by nature

- **consent** (approved/denied + notes) → the confirmation service (memorization, status, settlement);
- **tool-specific edits** (enabled hunks, a chosen variant) → `SharedContext` (the tool's own channel between its stages);
- **argument edits** → onto `call.Arguments` (persisted, visible in history).

### Effective arguments

`Effective = ArgumentsOverride ?? parsed(call.Arguments)`. **`ArgumentsOverride`** is an ephemeral `JsonNode?` carried on the context — a **hook channel** that affects only the tool's behaviour, distinct from the persisted `call.Arguments` (the model's intent, edited at approval). **All stages read the effective arguments** (so a lock and a specifier check match what actually runs). The effective set is exposed through a **derived accessor with keyed invalidation** — `IToolArgumentsAccessor.Effective`, cached by `(overrideReference, rawString)` — so a stale parse tree is unrepresentable rather than merely discouraged.

## Comments

<!-- appended conversation -->
