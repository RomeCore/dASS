# Lua hooks

Status: resolved
Type: grilling
Blocked by: 02

## Question

Scripts should be able to register hooks; the user's idea is a registration function taking a hook type and a function. This ticket settles the taxonomy of hook types; what a hook attaches to (the natural seam is the shared services, since there is no stage engine to hang off); the execution model (whether a hook may block, inspect, mutate, or reorder the flow, and where in a service's life it fires); the ordering when several hooks match the same type; and what happens when a hook fails — whether it aborts the invocation or is swallowed. This is feature goal eight.

## Answer

Hooks are a **further-expansion** topic. This ticket fixes the seam and the hook semantics and explicitly defers the rest.

### Seam

Hook points are **positional** (`BeforeX` / `AfterX`) around the pipeline stages — they are **hooks, not events**: a subscriber does not merely observe, it acts at a point in the flow. They are fired **by the stage service in whose life the point occurs** (nobody else knows the moment). The existing chat-execution hooks are the *opposite* model (fire-and-forget notifications) and are deliberately **not** the model here.

### Execution model

Hooks run **sequentially**; **each sees the previous hook's result** and can **interrupt the flow** or **override** it. Concretely: if the policy disallowed a tool but a hook allowed it, **the hook wins**. Hooks may also mutate the invocation (e.g. via `ArgumentsOverride`, the hook channel from ticket 07).

### Deferred (further expansion)

- the full taxonomy of hook points;
- the verdict shape returned by a hook;
- ordering when several hooks target the same point;
- failure handling (abort the invocation vs swallow);
- whether hooks run **sequentially or in parallel** — explicitly **still open**.

## Comments

<!-- appended conversation -->
