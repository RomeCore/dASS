# Lua hooks

Status: open
Type: grilling
Blocked by: 02

## Question

Scripts should be able to register hooks; the user's idea is a registration function taking a hook type and a function. This ticket settles the taxonomy of hook types; what a hook attaches to (the natural seam is the shared services, since there is no stage engine to hang off); the execution model (whether a hook may block, inspect, mutate, or reorder the flow, and where in a service's life it fires); the ordering when several hooks match the same type; and what happens when a hook fails — whether it aborts the invocation or is swallowed. This is feature goal eight.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->

- Related precedent (noted while charting): the chat execution loop already carries an ordered hook mechanism — collaborators registered as hooks and invoked, in registration order, at the prepare, response-completed, agent-finished and execution-finished points; memory reading and recording, chat naming and summarization ride it today. The scripted hook design should be measured against this precedent: the same registration-and-order shape, and a decision on whether scripted hooks extend that mechanism, mirror it, or attach at a different seam.
