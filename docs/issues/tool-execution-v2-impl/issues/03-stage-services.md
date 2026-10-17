# P3 — Stage services

Status: open
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

## Synchronization

`IToolSynchronizationService.RunAsync(ctx, inner)`:

- keys = `descriptor.StaticSyncKeys` + `descriptor.SyncAnalyzer?.AnalyzeSyncKeys(...)`.
- typed keys (`File` / `Directory` / `Custom`), hierarchical prefix conflicts
  (a held `Directory` conflicts with any `File` beneath it).
- canonicalization only (normalize, collapse a file under an already-requested directory); the analyzer is
  the one that absolutizes paths.
- atomic acquisition of the whole key set — no hold-and-wait, no deadlock; release in `finally`.
- the existing per-group `SemaphoreSlim` dictionary is deleted with `ToolExecutionService`.

## Filesystem analyzer

`FilesystemSynchronizationAnalyzer` on the filesystem tool family: derives `File`/`Directory` keys from the
path arguments (absolutized). This is the proof of the hierarchical model; other tools keep static keys.

## Policy and confirmation

- `IToolApprovalService.ApproveTool` folds into `IToolPolicyService`.
- `ToolPolicyReason` (`ToolDecisionSource` + message) is written onto the context by policy and read by
  confirmation.
- `ToolConsentPanel` stays the shell. `IConfirmationSink` is passed by the caller (P4/P5).

## Acceptance

- Unit tests: synchronization (atomic multi-key, prefix conflict, canonicalization, concurrent no-deadlock,
  cancellation while waiting), policy (mask merge, specifier combine, memorization precedence), accessor.
- `ToolExecutionService` still exists (it is rewritten as an orchestrator in P4), but its stages are gone:
  it now only sequences the services.
