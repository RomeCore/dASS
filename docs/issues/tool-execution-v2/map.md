# Tool execution v2 — wayfinder map

`wayfinder:map` (the file name itself is the marker; there is no label system).

## Destination

A locked, implementable **spec** for the v2 tool-execution core, published as `docs/issues/tool-execution-v2/spec.md`. It covers:

- the single **domain model** that both the chat tool world and the agent-task tool world project onto (a tool descriptor, a tool call, and the per-invocation context);
- the reusable **execution services** the orchestrators call;
- the **five orchestrators** — the full chat run, a chat tool restart, a tool command, an agent task, and a script call — and the invocation source that selects between them;
- the removal of the duplication between the chat execution path and the agent-task executor;
- the eight feature goals as sections: argument editing at approval; approval separated from execution; the richer synchronization model; unification with the task executor; the agentic approval helper; restart and cancellation of a single tool; cascade approval; Lua hooks.

The map ends at the spec. Implementation is a separate effort.

## Notes

**Domain / skills to consult**

- `grilling`, `domain-modeling` (defaults), `codebase-design` for the service seams.
- Repo conventions live in `AGENTS.md`; the tracker itself is `docs/issues/README.md`.

**Standing preferences for this effort**

- The effort is **planning only**: this map produces the spec; the refactor is handed off, not performed here.
- **Never mention competing agent runtimes** anywhere in `docs/issues/` or the spec (same rule as the slash-commands effort).
- Keep everything in the repo's own vocabulary; introduce no naming that the project does not already use or deliberately adopt.

**Settled premises (decided with the user during charting)**

- **Shape is simple decomposition plus orchestrators.** There is no generic stage engine and no declarative stage list: with only five ways to run a tool, a shared set of services plus thin per-source orchestrators is simpler and more honest. The nesting (synchronization wrapping the inner steps) is expressed directly in imperative code.
- **Synchronization is a scope, not a flat step.** It opens around the inner steps of an invocation and closes on exit.
- **Approval belongs to agent decisions.** An agent's tool choice is approved; a user's own action (a typed tool command, a tool restart) *is* the decision and need not re-ask. Whether trusted code (a script) also skips policy, and whether a hard floor survives for any of these, is ticket 13.
- Invocation sources are a first-class notion even without a stage engine: the source selects the orchestrator and how the context is filled.
- All eight feature goals are in scope; the core refactor is however many tickets it takes.
- **Decompose by concern, not by size.** The chat execution loop is large but cohesive — one job spread over many lines — and is *not* a target: at most its mechanical parts (the ordered hook runners, the failed-usage recording) move to a collaborator. The tool execution core is the opposite: several independent decisions sharing one body, which is exactly why it decomposes.
- **Hooks are not events.** The chat execution loop's hook list (memory, naming, summarization, message reveal) is a *notification* mechanism and is deliberately **not** the model for scripted hooks; those are interception points that can interrupt or override the flow (ticket 12).

## Decisions so far

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->


- [Spec assembly](issues/14-spec-assembly.md) — the destination spec is published at [`spec.md`](spec.md): the design plus Appendix A (concrete contracts), B (current→new map), C (per-stage value flow) and D (migration order). Validation is a stub initially; the approval helper is deferred (build the tool system without it). **The map is closed.**
- [Tool-command source (slash command to tool)](issues/15-tool-command-source.md) — a derived slash command like skills/sub-agents: `DerivedSlashCommandProvider<ToolInfo>` owns the source (`/tool:<name>`), the argument schema is generated from the tool's `ArgumentSchema` (designated main argument → the positional/rest slot), and the run is the `Command` orchestrator (user action: policy/confirmation skipped, hard floor honoured).
- [Trust model for non-agent invocations](issues/13-non-agent-trust-model.md) — agent decisions run the full pipeline; the user's own actions (`Command`, `ChatRestart`) skip policy/confirmation but honour the **hard floor** (`AlwaysDisallow`); **scripts bypass the pipeline entirely** — no policy, no confirmation, **no hard floor** (a deliberately powerful area), so the script-capability clause is moot.
- [Lua hooks](issues/12-lua-hooks.md) — hooks are a **further-expansion** topic. Seam: **positional** `BeforeX`/`AfterX` points around the stages, fired by the owning stage service; they are **hooks, not events** (the chat-execution hook list is the opposite model). Execution: sequential, each sees the previous result, can interrupt/override (a hook beats the policy). Deferred: point taxonomy, verdict shape, ordering, failure handling, sequential-vs-parallel.
- [Cascade approval](issues/11-cascade-approval.md) — a decision with a scope other than once cascades to the **sibling awaiting calls of the same batch** (via `IToolCallHost.ToolCalls`; tasks via `ToolCallConfirmationRequests`). Reach (this tool / same name / every tool — "same kind" = same tool name, matching the existing `(chatId, toolName)` key) and duration (Once/Session/Task/Always) are **orthogonal**, as is the resolution reach (this call / every awaiting call); the resolution cascade is a one-off yes, so memorization follows the memory reach, never the cascade.
- [Restart and cancel of a single tool](issues/10-restart-and-cancel.md) — a restart re-runs the whole `ChatRestart` sequence (ticket 03) on the same `IToolCall` (state reset), optionally with edited arguments (→ `call.Arguments`); resuming from a chosen stage is rejected (stage-engine). Cancellation is per-invocation: each `IToolCall` carries its own `CancellationTokenSource` (runtime-only, on the call so the UI can cancel one call) linked to the run token; cancelling one tool leaves the run and siblings alive; a tool awaiting approval is cancelled via its consent; produced output is kept.
- [Agentic approval helper](issues/09-agentic-approval-helper.md) — lives inside `ToolConsentPanel` (the universal shell); not a pipeline step; mode (`manual`/`assistant`/`worker`) is a per-tool setting (descriptor + `ToolChange` override path, like the other tool metadata) together with `LeadDelay`/`SettleDelay`/`ExtraPrompt`. Context = descriptor + effective args + decision/`ToolPolicyReason` + preview artifact + recent messages (`MessageQuoteRenderer`) + `ExtraPrompt`; configurable model. Output = `{ verdict (approve|disallow|not_enough_context), notes_for_user, notes_for_agent, requested_data (tool schema), override_agent_intent, new_arguments }`; `not_enough_context` escalates even in worker; `new_arguments` and `requested_data` are mutually exclusive per tool; `override_agent_intent` picks ticket 07's channel. Follow-ups: 08 exposes the `requested_data` schema; 01/`ToolChange` gain the helper config.
- [Unifying with the agent-task executor](issues/05-unify-with-task-executor.md) — the task tool path becomes a fifth thin orchestrator (`AgentTaskToolOrchestrator`, App-scoped) over the shared services; the duplicated block in `AgentTaskExecutor` is deleted, `ChatAgentTool` dissolves, `DelegateAgentTool` → `DelegateMainExecutor`. Task-specific remainder: message plumbing, the task-card confirmation sink, `MaxParallelToolCalls`, chat-trigger wiring, `OnlyResponse`.
- [Argument editing at approval](issues/07-argument-editing-at-approval.md) — two live channels: edit the persisted `call.Arguments` at approval, or set the ephemeral `ctx.ArgumentsOverride` (hook channel); both feed the single effective-arguments accessor. **Both are validated** against the schema (approval edits before confirmation; hook overrides at install — Lua hooks are not trusted), so no invalid set reaches the lock/specifier/executor. "Not designed to be edited" is a property of the approval surface, not a flag.
- [Separating approval from execution](issues/08-approval-execution-separation.md) — the confirmation step is core-owned (`IToolConfirmationService`); the tool stops awaiting/applying inside its executor and contributes a custom approval interface via a descriptor capability `IToolApprovalProvider.AwaitDecisionAsync` (hosted by the panel), gated by an optional `HandledDecisions` mask (a tool may handle only Ask, leaving Disallow to the service). `ctx` carries the decision + `ToolPolicyReason {Source, Message}`. Approval outputs flow by nature (consent→confirmation, tool edits→`SharedContext`, arg edits→`call.Arguments`). Effective args = `ArgumentsOverride ?? parsed(call.Arguments)`, read by **all** stages through a keyed-invalidating accessor.
- [Synchronization model](issues/06-synchronization-model.md) — keyed sync: static descriptor-declared keys plus per-invocation keys derived by a dedicated **sync-analyzer** capability (`IToolSynchronizationAnalyzer`, the fifth executor; it absolutizes paths). Keys are typed (File/Directory/Custom) and **hierarchical** (a Directory conflicts with Files beneath it). The synchronizer canonicalizes (never resolves paths) and acquires **atomically** — no hold-and-wait, no deadlock; prefix-aware conflict detection.
- [Message and host abstraction](issues/04-message-and-host.md) — `IToolCallHost { IReadOnlyList<IToolCall> ToolCalls }`, no id, implemented by the base `ChatMessage` (tool calls already live there — the broadening must not be re-narrowed) and the task's message. The core needs only the sibling calls; message/owner access is chat-specific and reached by casting the concrete chat context.
- [Orchestrators and invocation sources](issues/03-orchestrators-and-sources.md) — five thin separate orchestrators (ChatRun/ChatRestart/Command/AgentTask/Script) with explicit per-source sequences (no stage engine, no shared inner helper). Invocation source is a plain `ToolInvocationSource` enum tag on `ctx.Source`; origin flows through `ctx`. Sync scope opens after validation and closes before settlement, spanning `preview→confirmation→execution` and held across confirmation on purpose (preview pre-computes → execution commits; `SharedContext` carries the artifact).
- [Shared execution services and their scopes](issues/02-shared-services.md) — eight thin **Application-scoped** stage services the orchestrators sequence (streaming, validation, synchronization, preview, policy, confirmation, execution, settlement); `settlement` runs always (own service), and `policy` swallows specifiers **and** the pure approver. The `IToolExecutionContextFactory` rides on `IToolDescriptor` (not a service) and fixes the tool-dependency provider (chat scope / `TriggeredChat.Services` / root, failing loudly without a chat). Consent machinery survives, re-placed.
- [Domain model of the execution core](issues/01-domain-model.md) — two contracts plus a context: `IToolDescriptor` **carries** thin single-method executors (`Main`/`Preview`/`Streaming`/`Specifier`); `IToolCall` is the fat-but-neutral, persisted state bag; `IToolExecutionContext` is the per-invocation interface every tool now takes (rename A). `IToolExecutionResult` is a thin completion handle; `ReactiveToolResult`/`PreviewToolExecutionResult` are deleted and payloads are written into the call (projected lossily at the world boundary). Neutral `IToolArgumentStream`, drained inside the (universal) streaming stage. The `*ExecutorCreator` family and `DelegateAgentTool` collapse onto one `ToolMethodBinder`. Plain polymorphism, no generics.

## Not yet specified

<!-- in-scope fog you can't ticket yet; graduates as the frontier advances -->

- The Lua hook taxonomy: the full set of points, the verdict shape, ordering, failure handling, and sequential-vs-parallel.
- The fate of the existing public execution entry point and everything that calls it.
- Whether and how a web front end participates in the execution path.

## Out of scope

<!-- work ruled beyond the destination; closed, never graduates -->

- **Implementation and build** — this map ends at the spec; writing the code is a separate effort.
- Anything **beyond the tool-execution subsystem** (prompt pipeline, memory, model management, and so on).
- **Comparisons with other agent runtimes** — not written down anywhere in this effort.
