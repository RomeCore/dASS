# Follow-up messages accumulated while an agent run is in progress

Status: done
Type: task
Blocked by:

## Requested

- A queue of follow-up messages that can be filled while the chat is busy, persisted as part of the chat's
  additional data.
- A per-message injection level (immediately, after a message's tool batch, after one agent's cycle, after the whole
  agent sequence), with the lowest level across the queue winning.
- A follow-up must equal an ordinary message: it re-enters the multi-agent entry point instead of continuing a run.
- Slash commands run one at a time, one message per command, and the next message waits for it.
- Every `UserInput` carries a `CanExecuteCommand` flag, set to `true` everywhere until participant rights exist.

## What landed

The full design is [`docs/issues/follow-up-messages/spec.md`](../../issues/follow-up-messages/spec.md); every
implementation ticket under `docs/issues/follow-up-messages/issues/` is closed with an answer that records what was
actually built and where it deviates.

In short:

- One `FollowUpChatMessagesData` item per chat in `Chat.AdditionalData`, owned by `IFollowUpChatMessageQueue`.
- Three barriers in `ChatExecutionService` (message, agent, agent sequence) that end the run with a plain return, and
  `ChatOperationService` as the owner of the turn that drains the queue and generates once for the batch.
- The message is appended to the history before its queue entry is removed, and its command runs after the removal,
  so the queue only ever shows messages that have not reached the history.
- Chips above the input in the shared Avalonia UI, with a queue dialogue, and the mirrored WebUI behaviour including
  message editing.

## Open follow-up

`IChatExecutionStatusService.WaitUntilIdleAsync` has no timeout. Stopping a run that is waiting on a Lua form which
does not observe its tool token would leave stop-and-flush waiting forever without a report. Recommended: a ceiling
with a warning in the log.
