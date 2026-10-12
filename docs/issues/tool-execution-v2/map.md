# Tool execution v2 — wayfinder map

`wayfinder:map` (the file name itself is the marker; there is no label system).

## Destination

A locked, implementable **spec** for the v2 tool-execution core, published as `docs/issues/tool-execution-v2/spec.md`. It covers:

- the single **domain model** that both the chat tool world and the agent-task tool world project onto (a tool call, a tool descriptor, a tool result);
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

## Decisions so far

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->

*None yet — the map was just charted.*

## Not yet specified

<!-- in-scope fog you can't ticket yet; graduates as the frontier advances -->

- The taxonomy of Lua hook types, and the exact set of seams a hook may attach to.
- The approval helper's internals: which model it uses, its prompt, how it selects context, and how much of the tool's own output it sees.
- Restart semantics at full resolution: replay the whole sequence versus resume from a chosen point.
- The shape of a script's declared capabilities, if capabilities are chosen over a hard floor.
- The fate of the existing public execution entry point and everything that calls it.
- Where the approval-helper and cascade settings physically live (which settings category, chat versus application).
- Whether and how a web front end participates in the execution path.

## Out of scope

<!-- work ruled beyond the destination; closed, never graduates -->

- **Implementation and build** — this map ends at the spec; writing the code is a separate effort.
- Anything **beyond the tool-execution subsystem** (prompt pipeline, memory, model management, and so on).
- **Comparisons with other agent runtimes** — not written down anywhere in this effort.
