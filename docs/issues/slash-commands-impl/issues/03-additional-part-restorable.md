# 03: Add `AdditionalMessagePart.IsRestorable`

Status: resolved
Type: task
Blocked by:

## What to build

Give message parts a flag that decides whether editing a message restores the part into the draft input state.
Default `true` preserves today's behaviour; command-injected parts (Stage 3) will set it `false` so an injected
skill body is not cloned back into `UserInputState` when the user edits the message.

## Acceptance criteria

- [x] `AdditionalMessagePart.IsRestorable { get; set; } = true` exists and is persisted/round-trippable like the other part flags.
- [x] The edit/restore path skips parts with `IsRestorable == false`.
- [x] No existing part changes its effective behaviour (default `true`).
- [x] The solution builds.

## Answer

- `AdditionalMessagePart.IsRestorable { get; set; } = true` added (serialized like the other part flags).
- `AdditionalChatDataCollection.GetRestorableParts()` returns the message parts with `IsRestorable` set.
- `UserInputViewModel.EditMessage` now restores `AdditionalChatDataCollection.GetRestorableParts()` (cloned) instead of
  only `AttachmentMessagePart` (the old `OfType<AttachmentMessagePart>()` filter). Today the only `AdditionalMessagePart` subclasses are `NativeAttachmentMessagePart`
  and its `AttachmentMessagePart`; since `NativeAttachmentMessagePart` is never instantiated on its own, behaviour is
  unchanged for existing parts — the filter only adds the opt-out.

Tests: `tests/LLMDesktopAssistant.Tests/MVVM/AdditionalChatDataCollectionRestoreTests.cs` (default `true`; the filter
returns restorable chips and skips non-part data and opted-out chips). Full suite green.
