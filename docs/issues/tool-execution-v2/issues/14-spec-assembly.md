# Spec assembly

Status: resolved
Type: task
Blocked by: 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 15

## Question

Assemble the destination spec from every resolved ticket in this map, into a single implementable document at `docs/issues/tool-execution-v2/spec.md`. Nothing new is decided here: this ticket only gathers the answers into one coherent whole, in the order a reader needs them, and closes the map.

## Answer

The specification is published at `docs/issues/tool-execution-v2/spec.md`: the design (§1–18) plus **Appendix A** (concrete contracts — every interface, member and signature), **Appendix B** (current → new map: deleted / reshaped / added / kept), **Appendix C** (per-stage value flow) and **Appendix D** (migration order).

Two implementation notes settled here:

- `IToolValidationService`'s initial implementation is a **stub that always returns valid**.
- The **agentic approval helper** (and its rendering) is **deferred**: the tool system is implemented first **without** the helper.

**This closes the map — its destination (a locked, implementable spec) is reached.**

## Comments

<!-- appended conversation -->
