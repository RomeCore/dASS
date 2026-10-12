# Trust model for non-agent invocations

Status: open
Type: grilling
Blocked by:

## Question

An agent's tool choice is approved; a user's own action (typing a tool command, restarting a tool) and a running script's nested call are not agent decisions. This ticket settles whether the non-agent paths run policy and approval at all, and whether any hard floor survives — whether a script, a restart, or a tool command may run a tool the user has explicitly disallowed. The open question is the script case in particular: whether nested calls bypass the pipeline entirely, respect a hard deny only, or are gated by capabilities the script declares. It also settles how the entry point — whatever launches the script — surfaces what the script can do, so that a hurried approval is still an informed one. It starts from the premise that the user's own actions need no approval, and covers the boundary cases where that premise is not enough.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
