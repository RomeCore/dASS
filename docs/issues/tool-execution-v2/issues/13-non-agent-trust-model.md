# Trust model for non-agent invocations

Status: resolved
Type: grilling
Blocked by:

## Question

An agent's tool choice is approved; a user's own action (typing a tool command, restarting a tool) and a running script's nested call are not agent decisions. This ticket settles whether the non-agent paths run policy and approval at all, and whether any hard floor survives — whether a script, a restart, or a tool command may run a tool the user has explicitly disallowed. The open question is the script case in particular: whether nested calls bypass the pipeline entirely, respect a hard deny only, or are gated by capabilities the script declares. It also settles how the entry point — whatever launches the script — surfaces what the script can do, so that a hurried approval is still an informed one. It starts from the premise that the user's own actions need no approval, and covers the boundary cases where that premise is not enough.

## Answer

Approval belongs to **agent decisions** only. The three source families split as:

- **Agent decisions** (`ChatRun`, `AgentTask`) — the agent's tool choice goes through the **full pipeline** (policy → specifiers → confirmation).
- **The user's own actions** (`Command`, `ChatRestart`) — the user *is* the decision, so they **skip policy and confirmation**, but a **hard floor survives**: a tool explicitly marked `AlwaysDisallow` (an equivalent hard deny) is still refused.
- **Scripts** (`Script`, including nested calls from Lua / CSX / Python) — **bypass the pipeline entirely**: no policy, no confirmation, **no hard floor**. Scripts are a deliberately powerful — and partly dangerous — area, and that is by design.

Because scripts bypass the pipeline, the "entry point surfaces what a script can do" clause is **moot**: there is no script-declared capability set and no approval to inform.

## Comments

<!-- appended conversation -->
