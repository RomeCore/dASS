# Agentic approval helper

Status: open
Type: grilling
Blocked by: 08

## Question

An agentic helper that lives inside the approval interface and can advise on, or decide, a tool call on the user's behalf. Three modes: manual (the user decides, and the panel offers a "consult" button and a "confirm through the helper" button), assistant (it starts on its own, fills notes as the user, hints the user, and highlights a recommended button), and worker (it confirms or rejects outright). This ticket settles the modes and their transitions; the configurable lead delay before it starts and settle delay before an automatic decision; the extra prompt given to the helper; the "not enough context" outcome when it cannot judge; the settings model (shared, plus per-tool overrides); and the two hosting forms — implicit inside the tool-call card, and explicit inside a tool-provided interface. The helper is not a step of the execution pipeline; it lives inside the approval interface only. This is feature goal five.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
