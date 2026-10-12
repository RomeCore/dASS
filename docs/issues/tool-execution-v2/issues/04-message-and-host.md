# Message and host abstraction

Status: open
Type: grilling
Blocked by: 01

## Question

The execution path must stop being tied to the assistant message specifically and work against any chat message that can host a tool call; the agent-task side has its own message type. This ticket settles the abstraction both sides satisfy — what the tool-call host is, what the core actually needs from it, and how the already-broadened message target from the slash-command effort is honoured rather than re-narrowed.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
