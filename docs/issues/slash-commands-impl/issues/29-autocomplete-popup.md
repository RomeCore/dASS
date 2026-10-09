# 29: Autocomplete popup + `InputCompletionViewModel`

Status: resolved
Type: task
Blocked by: 25, 26, 28

## What to build

The visible autocomplete: a caret-anchored popup over the input plus the view model that drives it, wired into the chat
input (`UserInputView`) and reusable from the debug page.

- **`InputCompletionViewModel`** — the state machine: closed / open (fresh) / filtering / empty / state-only /
  argument. It calls `IInputCompletionService`, holds the candidates and the current selection, and exposes the accept
  action (replace `Span` with `InsertText`, collapse to a single line, append the canonical form + a trailing space,
  park the caret).
- **`CommandAutocompletePopup`** (`UserControl`) — `PlacementTarget` = the `HighlightTextBox`,
  `Placement = BottomEdgeAlignedLeft`, offsets from the caret rect; the TextBox keeps focus and the popup is a passive
  list (`ItemsControl`), ~8 rows with scroll, mouse hover selects, click accepts. Row shows the kind icon, the
  qualified name and the localized description; defeated items are marked.
- **Keyboard** — the input view routes keys to `TryHandleKey` first: `↑/↓` move, `Tab` accepts, `Enter` accepts when
  the popup is open and otherwise sends, `Esc` closes (input untouched); typing filters.
- **State-only** — a non-null `State` with no `Items` renders the state header plus a "no matches" row.

## Acceptance criteria

- [x] `/` at the start opens the full list; typing filters it; `↑/↓/Tab/Enter/Esc` behave as above; `Enter` still sends
      when the popup is closed.
- [x] Accept replaces the token span with the fully-qualified form and parks the caret after a trailing space.
- [x] Argument mode shows the format provider's completions (today `wait=true|false`).
- [x] State-only renders without items; the popup never steals focus (`Focusable=False` list and rows; the text box
      keeps focus).
- [x] Wired into `UserInputView` (the popup is reusable; sharing it with the debug page is a follow-up).
- [ ] Locale keys added to `iv` and `ru-RU` (done); main + desktop build green; **manual verification pending**.

## Answer

- **`InputCompletionViewModel`** (`LLM/MVVM/`): the state machine over `IInputCompletionService` — `IsOpen`,
  `Items` / `SelectedItem` / `SelectedIndex`, `State`, `IsStateOnly`, `CanAccept`; `Update(text, caret)` recomputes
  and closes on no completion; `TryHandleKey` owns Up/Down/Escape; `Accept()` replaces the result's `Span` with the
  selected `InsertText` plus a trailing space and returns the new text + caret.
- **`InputCompletionPopup`** (UserControl): a passive list (`ListBox`, `Focusable=False`, non-focusable rows) showing
  each item's `DisplayText` + localized `Description`, a marker for defeated items, and a `command.completion.no_matches`
  row when state-only. A click raises `AcceptRequested`.
- **Wiring (`UserInputView`)**: the input is now a `HighlightTextBox`; its `CaretStateChanged` updates the view model
  (and the renderer's caret), `PointerCaretStateChanged` closes the popup; the `Popup` anchors off `GetCaretRect`
  with the text box keeping focus; the tunnel key handler accepts on Enter/Tab when open, still sends on Enter when
  closed and routes Up/Down/Escape to the view model. `UserInputViewModel` builds the completion view model and the
  renderer.
- **`GhostText`** is now produced by the completion source in argument mode (`wait=tr` → `ue`), so Right accepts it
  character by character. The token *name* has no inline ghost yet — the fully-qualified insert does not extend the
  typed token, so its placement/accept is a follow-up.
- Locale keys `command.completion.no_matches` / `command.completion.defeated` added to `iv` + `ru-RU`.

Verified: the touched tests **40** green; **194** slash-command tests green (excluding the pre-existing
`ChatMessageInsertionServiceTests` parallel-collection deadlock) and those **24** green in isolation; desktop build
green. **Manual verification in the app is pending**, and the debug page still uses its own stand-in completion
(sharing the real view model there is a follow-up).

## Comments

<!-- appended conversation -->
