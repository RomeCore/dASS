# 04: Move `Error` up to `ChatMessage`

Status: resolved
Type: task
Blocked by:

## What to build

Every message can carry an error — commands (Stage 2) and any future user-message mutation attach runtime errors to the
target message, which is not necessarily an `AssistantMessage`. Lift `Error` from `AssistantMessage` to `ChatMessage`
and surface it in the UI for user messages too.

## Acceptance criteria

- [x] `ChatMessage.Error : string?` exists (mutable, persisted; the DB model already stores `Error`).
- [x] `AssistantMessage.Error` is removed; `AssistantMessage` inherits the base property with no behaviour change.
- [x] `MessageViewModelBase` exposes `Error` sourced from `Message.Error` and reacts to changes.
- [x] `UserMessageView` renders the error like `AssistantMessageView` does.
- [x] The solution builds; assistant-message error display is unchanged.

## Answer

- `ChatMessage.Error : string?` added (mutable, `SetProperty`); the DB model already stored `Error`, so nothing changed
  on the storage side.
- `AssistantMessage` no longer declares `Error` — it inherits the base one; `ChatExecutionService` and
  `MessageDatabaseSynchronizer` keep compiling unchanged.
- The error surfaced in MVVM moved from `AssistantMessageViewModel` to `MessageViewModelBase` (reads `Message.Error`
  once and subscribes to `ChatMessage.Error` changes); `UserMessageView` now shows the same red error label that
  `AssistantMessageView` already had.

No new tests (as agreed). Full suite: 790 passed, 1 skipped; main + desktop builds green (Blazor's
`AssistantMessageComponent` keeps binding `Message.Error` through the base class).
