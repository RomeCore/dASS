# Unifying with the agent-task executor

Status: open
Type: grilling
Blocked by: 03

## Question

The agent-task executor currently repeats, almost step for step, the chat execution path: preview, policy, specifiers, approval decision, memorization, consent, execution. This ticket settles how the task path is re-expressed on top of the shared orchestrator and services, what genuinely remains specific to tasks (their own message plumbing, and the confirmation requests surfaced on the task card), and what is deleted from the task executor once the duplication is gone. This is feature goal four.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
