# 02: Barriers and the early exit in the execution loops

Status: resolved
Type: task
Blocked by: 01

## What to build

The three injection barriers in `ChatExecutionService`, and the normal-return exit that carries them up the call
chain. The executor learns to say "I stopped because the queue is eligible" - and nothing else.

See [`spec.md`](../spec.md) section *Barriers and the early exit*.

## Contracts

- `GenerateMessageResponseAsync` returns `(Domain.AssistantMessage Message, bool Interrupted)`.
- `GenerateResponseWithAgentAsync` returns `Task<bool>` - `true` means interrupted.
- `GenerateResponseAsync` keeps `Task` and returns early; it does not throw and does not cancel.

## Rules

- `Message` barrier: right after `finally { await Task.WhenAll(toolExecutionTasks); }`, before `return`. The message
  is already `Success`, the usage is recorded, and the response-completed hooks have run.
- `Agent` barrier: inside the `response.ToolCalls.Count == 0` branch, **after**
  `RunAgentExecutionFinishedHooksAsync`. The agent finished its cycle honestly and keeps its hooks.
- `AgentSequence` barrier: at the top of the loop, **before** agent selection. This placement is what stops a
  follow-up from being reported as "agent selection failed" with `cycles == 0`.
- No new exception type, no new `catch` clause, no `Cancel()`. The state is committed at every barrier before the
  check, so the interruption is invisible in the history except for the missing continuation.
- `IChatExecutionService` may now depend on `IFollowUpChatMessageQueue` (it must not depend on
  `IChatMessageInsertionService`).

## Acceptance

- With an entry at `Message` level, a tool-calling message finishes, its tools are awaited and completed, and the
  run returns without a second message cycle.
- With an entry at `Agent` level, the agent's execution-finished hooks fire and the next agent is not selected.
- With an entry at `AgentSequence` level, the whole sequence returns before selecting the next agent.
- No entry, no change in behaviour: the same messages, hooks, toasts and status transitions as today.
- An exception thrown by the model or a tool still propagates unchanged (no barrier check swallows it).

## Not in scope

Draining, insertion, the turn loop.

## Answer

Three barriers landed where the ticket put them: the message barrier after `Task.WhenAll(toolExecutionTasks)`, the agent barrier after `RunAgentExecutionFinishedHooksAsync`, the sequence barrier before agent selection. `GenerateMessageResponseAsync` returns `(Message, Interrupted)` and `GenerateResponseWithAgentAsync` returns `bool`; no exception type was introduced, because the three generic `catch` blocks would have logged a sentinel at every level and the message-level `OperationCanceledException` handler would have marked a successful message as cancelled.
