# 10: WebUI parity

Status: resolved
Type: task
Blocked by: 07, 08, 09

## What to build

The WebUI side of the same feature, in one change, with message editing last.

See [`spec.md`](../spec.md) section *WebUI*.

## Rules

- `ChatPage.razor`: the input is no longer `disabled="@_isGenerating"`. Enter sends **with** generation and
  Ctrl+Enter without - today it is the opposite (Enter sends without generation), which is a bug this ticket fixes.
- The queue tab and its dialog are shared state: every participant sees the same entries with their sender, and every
  participant can queue, delete and clear. This is what closes AR-0027 by construction: what another participant does
  never disables this participant's box or steals its focus.
- `UserInput.CanExecuteCommand` is set explicitly by the WebUI on every send (queue path included) and is `true` until a
  participant-rights model exists.
- Message editing: a `UserMessageComponent` action loads the message into the input (desktop parity, not a modal) and
  commits through `IChatOperationService.SendEditedUserInputAsync`, which is a turn with the idle wait. Editing ships
  after the queue works in the browser.
- Submission still goes through `IChatOperationService`; the component holds no execution logic.

## Acceptance

- Two browsers, two accounts: A runs a generation, B types and presses Enter, B keeps the caret and its text, and the
  entry appears in the shared queue tab for both.
- B presses Enter on an empty box during A's run: A's run stops and the queue is injected; the result is visible to both.
- Ctrl+Enter in the browser sends without generation.
- Editing a message in the browser replaces it, truncates the tail, and continues generation from the new branch.
- No WebUI path can execute a command with `CanExecuteCommand = false`.

## Not in scope

Participant permissions, per-participant private queues (the queue is shared by design).

## Answer

The box is never disabled, Enter generates and Ctrl+Enter does not (the inverse behaviour was a bug), the chip and the dialogue are shared state with the sender shown, `CanExecuteCommand` is set on every send, and message editing loads the message into the input. Two additions beyond the ticket. First, only the author sees the edit action: editing truncates the branch for every participant, so until a rights model exists the cheap honest rule is "own messages only". Second, both send buttons stay available during a run, so the queue is reachable with the mouse and not only through Enter. The edit is held as the `BranchedMessage` and its identity is checked before commit, so a history that moved under the edit is not silently rewritten.
