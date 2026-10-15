---
name: code-trace
description: Stress-test a design by writing a runtime trace instead of a diagram - start at one entry point and, at every key call, paste the callee's body inline and keep going into it. The discipline is that every hop must name the argument it passes and the value it returns, so hand-waving about context cannot survive it. Reach for it when the hard part is what gets passed where - a context, state or request object crossing stages, orchestrators, services, pipelines or middleware - when you suspect a sequence is written more than once across callers, or when a diagram stays vague because the seam is in the plumbing, not the boxes. Used well it turns "the pipeline handles it" into a named value, a named owner and a named seam.
user-invocable: true
disable-model-invocation: false
injection-mode: default
---

# Code trace

A **trace** is the code as the machine executes it: start at one entry point, and **every time execution reaches a key function, stop and paste that function's body inline, in place**, then keep going into it. It is a debugger's *step into*, written down. The output is not a diagram, a call graph, or pseudocode that gestures at the seams - it is one linear walk in which every hop must name exactly what it passes and what comes back.

Why it works: a diagram lets you draw a box and hand-wave what is inside the arrows. A trace forbids it - the moment you must write the argument at the call site, the hand-waving dies.

## When to reach for it

- the hard part of the design is **what gets passed where** - a context, state, environment or request object crossing stages, orchestrators, services, middleware, pipeline stages, decorators;
- you suspect the same sequence is written **more than once** across callers and want it side by side;
- a diagram stays vague because the seam is in the plumbing, not in the boxes;
- you are designing or reviewing **before** implementing and want to cheaply falsify the shape;
- a collaborator is handed "everything" and you want to see exactly what it uses.

Not for: pure data modelling (types with no flow), UI layout, single-function logic, or anything where a signature already tells the whole story.

## The method

### 1. One scenario, not all of them

Pick the single most complete path through the system - the one that touches the most stages. Trace that one. Every other path is a diff against it (step 5).

### 2. Start at the entry, in real code

Write the entry point as it really is: real type names, real signatures, minimal bodies. Build the objects the walk needs the way the code builds them.

### 3. Expand at every key call

At each call to a collaborator, **stop and paste the callee's body inline**, indented and marked as an expansion, then keep going inside it. Recurse until you reach a leaf that does real work (the logic itself, a loop, an I/O call).

- A "key" call is anything that could hide a seam: a service, a stage, an orchestrator, a wrapper, a template method, a callback.
- **Never write `await service.DoStuff()` and move on** - that is the one thing the technique exists to prevent.
- Keep the repo's real names; invent nothing.

### 4. Follow the values, not only the calls

Expanding forces every call site to name its arguments. As you write them down, keep a running note of the **value flow**: which value is born where, which hop carries it, which hop mutates it, and which hop recomputes something an earlier hop already had. Control flow is what a diagram draws; value flow is what the trace adds, and it is where the friction usually hides - a hop that rebuilds a value instead of receiving it, or a collaborator handed more than its body consumes.

### 5. Diff the other paths

Write every remaining scenario as a diff from the traced one - only the lines that change. If two paths differ by a flag rather than a line, say so out loud: that flag is a decision to revisit.

### 6. Read it back and name what it exposed

The payoff is the list the trace forces you to write down - the deliverable is the findings, not the code. Findings are always about **owners and boundaries**: who holds a value, who is handed it, and which hop is the only one that should have known. Write them down; a worked set is in [EXAMPLE.md](EXAMPLE.md).

**Done when:** every key call on the traced path is expanded to a leaf, the value flow is noted at every hop, the other paths are written as diffs, and the findings are written down.

## What bites

- **Stopping at the first abstraction.** `await pipeline.Run()` with no expansion is a diagram wearing code's clothes.
- **Tracing all paths at once.** It bloats and hides the shape. One path, then diffs.
- **Inventing types.** Fake names let fake seams survive. Use the ones already in the repo.
- **Keeping the trace as an artifact.** It is disposable; when a decision changes, redraw it - cheaper than maintaining it.
- **Skipping the value flow.** The code alone is not the payload; what crosses each hop is.

## Neighbours

- Stress-testing the decisions themselves: the `grilling` skill.
- Deep-module seams and testability vocabulary: the `codebase-design` skill.
- Settling the words the trace speaks in: the `domain-modeling` skill.
- A worked trace, and the findings it forced: [EXAMPLE.md](EXAMPLE.md).
