# Domain model of the execution core

Status: open
Type: grilling
Blocked by:

## Question

The execution core needs a single domain model that both the chat tool world and the agent-task tool world project onto: a tool call, a tool descriptor, and a tool result that belong to neither world's concrete types. This ticket settles what those three domain concepts are and what each one carries; how the existing concrete types on both sides become projections onto them; where the metadata currently duplicated across the two worlds ends up (expected behaviour, approval level, policy mask, specifiers and their modes); and whether the core speaks through plain polymorphism or through generics. The chat-only richness — live progress, streaming status, appended additional data, custom result views — must not leak into the shared core, yet the chat path must still reach it. This is the root of the whole effort: everything else projects onto what is settled here.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
