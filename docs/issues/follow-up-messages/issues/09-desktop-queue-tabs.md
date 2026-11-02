# 09: Queue chips and dialog (Avalonia UI)

Status: resolved
Type: task
Blocked by: 08

## What to build

The visible queue: a row of chips above the divider between the history and the input, the queue dialog, and the edit
mode turned into a second chip.

See [`spec.md`](../spec.md) section *Avalonia UI (Desktop and Android)*.

This is the shared Avalonia UI in `src/LLMDesktopAssistant`, so Desktop and Android get it at once; nothing here
belongs to `src/LLMDesktopAssistant.Desktop`.

## Where it sits

`ChatView.axaml` is `Grid RowDefinitions="*,Auto"`: row 0 holds the messages `Panel` (with `MessagesScrollViewer`, the
`ScrollToBottomButton` at bottom-right with `Margin="32,16"`, and `AgentTasksButton` at top-right), row 1 is the
`Border` with `BorderThickness="0,1,0,0"` that hosts `ChatBottomPanel`.

The row is an overlay in row 0: `VerticalAlignment="Bottom"`, `HorizontalAlignment="Left"`, `ZIndex` above the
scroll content, and a right margin that keeps it clear of the scroll-to-bottom button's bottom-right box - the chips
must never cover that button and must not take layout space from the history.

A chip is a button, not a tab: no `TabControl`, no selected state. A dismissible chip carries a nested close button
inside the same visual block.

## Contracts

- `UserInputViewModel` owns `UserInputChipsViewModel` (exposed as `UserInput.Chips`): the row is the input's chrome,
  because both chips are about the input - one holds messages typed but not sent, the other a message loaded for
  editing. It is named for the input, not for the queue: "follow-up" describes only one of the two chips.
- The view model exposes the chip items and their commands: the queue chip (`Count`, visible at `N >= 1`, title
  `chat.follow_up.chip` formatted with the count), its nested close action (`chat.follow_up.clear`), and the edit chip
  (visible while `UserInputViewModel.EditingMessage != null`) whose body brings the message into view and whose nested
  close cancels the edit.
- `FollowUpQueueDialogViewModel` + view: entries in composition order, each with a text preview, the sender login, the
  level and a delete action; the dialog closes when the queue empties.
- The close button clears through `ConfirmDialogViewModel` with `IsDanger`, title and description localized, confirm
  and cancel via `LocalizationManager.LocalizeStatic`.

## Rules

- Deleting from the dialog goes through `IFollowUpChatMessageQueue.Remove`, so the persisted item stays the single
  source of truth; the tab count follows `Changed`.
- The row hides entirely when there is nothing to show (empty queue, not editing).
- The edit chip raises a bring-into-view request that the message list resolves for `EditingMessage`'s container; it
  does not scroll by index arithmetic.
- No reordering, no editing of a queued entry's text, no per-entry level change in the dialog (v1).
- Every user-facing string is a `Locale` key; no literals in the view.

## Acceptance

- Typing while a run is going makes the chip appear with the right count and does not shift the history.
- The chip does not overlap the scroll-to-bottom button at any window width down to the minimum, on desktop and on
  Android.
- The dialog deletes one entry, the count drops, and a restart does not bring it back.
- The nested close button asks for confirmation and then empties the queue in one go.
- Entering edit mode shows the edit chip, and clicking its body brings the edited message into view even when it is
  scrolled far up; its nested close cancels the edit.

## Not in scope

WebUI (ticket 10).

## Answer

Landed as chips in the shared Avalonia UI (Desktop and Android together). The queue chip shows the count and opens the dialogue with per-entry delete; its nested close clears everything behind `ConfirmDialogViewModel`. The edit chip brings the message into view through `MessageSequenceViewModel.BringIntoViewRequested` and its nested close cancels the edit. Adjusted after the first live look: an accent border, the row flush with the divider (`Margin="16,0,96,0") and on the left, and the toolbar's separate cancel-edit button removed because the chip owns that action now. Keeping clear of `ScrollToBottomButton` is a right margin of 96.
