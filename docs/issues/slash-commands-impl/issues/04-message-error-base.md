# 04: Move `Error` up to `ChatMessage`

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Every message can carry an error — commands (Stage 2) and any future user-message mutation attach runtime errors to the
target message, which is not necessarily an `AssistantMessage`. Lift `Error` from `AssistantMessage` to `ChatMessage`
and surface it in the UI for user messages too.

## Acceptance criteria

- [ ] `ChatMessage.Error : string?` exists (mutable, persisted; the DB model already stores `Error`).
- [ ] `AssistantMessage.Error` is removed; `AssistantMessage` inherits the base property with no behaviour change.
- [ ] `MessageViewModelBase` (or the shared message VM) exposes `Error` sourced from `Message.Error` and reacts to changes.
- [ ] `UserMessageView` renders the error like `AssistantMessageView` does.
- [ ] The solution builds; assistant-message error display is unchanged.
