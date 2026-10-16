# Orchestrators and invocation sources

Status: resolved
Type: grilling
Blocked by: 02

## Question

There are five ways to run a tool: the full chat run (agent-initiated), a chat-side tool restart, a tool launched from a slash command, an agent-task tool call, and a script-initiated call. This ticket settles the exact sequence each orchestrator runs, where the synchronization scope opens and closes around the inner steps, what an invocation source is and how it is represented and carried through the context, and whether the restart and tool-command sequences (which look identical) are two orchestrators or one with two faces. It also settles how much of the sequence is shared versus how much each source fills in for itself.

## Answer

The core is driven by **five thin, separate orchestrators** — one per way a tool can run. Restart and tool-command keep identical sequences today but stay **two** orchestrators.

| Source (`ToolInvocationSource`) | Sequence |
|---|---|
| `ChatRun` | streaming (Cyclic) → validation → **sync{** preview → policy → confirmation → execution **}** → settle |
| `ChatRestart` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `Command` | streaming (Once) → validation → sync{ preview → execution } → settle |
| `AgentTask` | streaming (drain only, no analyzer) → validation → sync{ preview → policy → confirmation (implicit) → execution } → settle |
| `Script` | streaming (no stream → no-op) → validation → sync{ preview → execution } → settle |

### Invocation source

A plain tag: `enum ToolInvocationSource { ChatRun, ChatRestart, Command, AgentTask, Script }`, carried on `ctx.Source`. A per-entry front controller picks the orchestrator by the tag. All origin context (chat, message, task, confirmation sink, effective behaviours) flows through separate `ctx` fields — there is no second container beside `ctx`.

### Synchronization scope

Opens **after** streaming/validation and closes **before** settlement; it spans the whole inner sequence `preview → policy → confirmation → execution`. The lock is deliberately held **across confirmation**: a preview may pre-compute the resulting file (that artifact travels to execution through `SharedContext`), so `preview → execution` is **one atomic operation on the resource** — two edits of the same file must never interleave. The tighter variants (confirmation outside the lock; release-and-reacquire) are left to the synchronization model (ticket 06) and cascade (ticket 11).

### Shared vs per-source

- **Shared:** the eight stage services, and the synchronization higher-order wrapper (`_sync.RunAsync(ctx, inner)` — it receives a lambda and knows nothing about stages).
- **Per-source (orchestrator-owned):** the assembled `ctx` (via the descriptor's context factory plus the source inputs), `EffectiveBehaviours`, the confirmation sink, the streaming mode, parallelism/cancellation, and the set and order of stages — expressed as **explicit imperative code** per orchestrator. No stage engine, no declarative stage list, no shared inner-sequence helper: five orchestrators duplicating ~4 lines each is cheaper than one clever engine that starts to rot.

## Comments

<!-- appended conversation -->
