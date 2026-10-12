# Orchestrators and invocation sources

Status: open
Type: grilling
Blocked by: 02

## Question

There are five ways to run a tool: the full chat run (agent-initiated), a chat-side tool restart, a tool launched from a slash command, an agent-task tool call, and a script-initiated call. This ticket settles the exact sequence each orchestrator runs, where the synchronization scope opens and closes around the inner steps, what an invocation source is and how it is represented and carried through the context, and whether the restart and tool-command sequences (which look identical) are two orchestrators or one with two faces. It also settles how much of the sequence is shared versus how much each source fills in for itself.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
