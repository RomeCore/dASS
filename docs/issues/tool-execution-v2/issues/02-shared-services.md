# Shared execution services and their scopes

Status: resolved
Type: grilling
Blocked by: 01

## Question

Enumerate the reusable services that make up the decomposed core — the units every orchestrator calls: argument validation, synchronization, preview, policy and specifiers, confirmation, execution, and settlement (status and result reporting). For each one settle its responsibility, its boundaries, what it needs from the domain model, and which dependency-injection scope it lives in (chat versus application). This ticket also settles the fate of the existing consent machinery — the approval decision service, the memorization service, the consent panel, and the persisted "always" decisions: do they survive as they are, move into the new core, or get replaced. This is where the duplication between the two worlds is actually removed.

## Answer

The shared core is a set of **eight thin, stateless stage services** (interfaces), all **Application-scoped**, each taking its state from `IToolExecutionContext`. The orchestrators sequence them; the services hold no per-chat state.

| Service | Responsibility |
|---|---|
| `IToolStreamingService` | drain the argument stream, run the optional streaming analyzer → `call.Arguments`, status icon/title |
| `IToolValidationService` | tolerant-parse + schema-validate the arguments → `ctx.ParsedArgs` (synchronous) |
| `IToolSynchronizationService` | keyed higher-order scope around the inner steps (locks live only here) |
| `IToolPreviewService` | run the preview capability, fold its control outcome into the call |
| `IToolPolicyService` | `merge PolicyMask → SpecifierEngine → memorized decision → approve → final decision` |
| `IToolConfirmationService` | the confirmation step — explicit (user TCS via sink) and implicit (self-handled) — plus memorization and "always" persistence |
| `IToolExecutionService` | run `descriptor.Main` (returns `ToolExecutionResult`; its task *is* the completion) |
| `IToolSettlementService` | finalize the call (default content per status, notes, `CompletionToken`), run **always** by the orchestrator in a `finally` |

### Decisions

- **`policy` and `specifiers` are one service**, and the existing pure `IToolApprovalService` (`ApproveTool`) is **folded into it** — the chain `merge → specifier → memorized → approve → decision` lives linearly in one stage.
- **`settlement` is its own service** and runs **always** (short-circuits included); it is never a tail of execution.
- **Scopes:** every stage service is **Application** (stateless; state flows through `ctx`); the chat orchestrators are **Chat**; the service that replaces the tool-execution logic inside `AgentTaskExecutor` is **Application** (agent tasks are app-scoped and only attach to a chat for dispatching).
- **Consent machinery survives, re-placed:** the pure approver folds into `IToolPolicyService`; `IToolMemorizationService` stays Application-scoped (used by policy and confirmation, and extended for cascade); `ToolConsentPanel` survives as the **universal confirmation shell** — it appears everywhere (chat and agent tasks) and hosts a tool's custom approval content inside it, and it is where the approval helper lives; the persisted "always" survives, its call site moving into the confirmation stage.
- **Context creation:** `IToolExecutionContextFactory` is an **optional property carried by `IToolDescriptor`** (not a service). `CreateContext(in ToolContextCreationParams)` merges the source-supplied invocation inputs with the tool's home scope captured in the factory's constructor. The service provider is the **most-scoped available** (the chat scope for a chat run, `TriggeredChat.Services` for an attached task, the root otherwise); a missing chat-scoped dependency **fails loudly**. Because addons are frozen, the factory is attached on the runtime projection via `Clone → mutate → Freeze`.
- **`ToolPolicyMask`** is the type of the effective-behaviours input (not a tuple).

## Comments

<!-- appended conversation -->
