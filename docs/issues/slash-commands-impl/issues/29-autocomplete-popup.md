# 29: Autocomplete popup + `InputCompletionViewModel`

Status: open
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

- [ ] `/` at the start opens the full list; typing filters it; `↑/↓/Tab/Enter/Esc` behave as above; `Enter` still sends
      when the popup is closed.
- [ ] Accept replaces the token span with the fully-qualified form and parks the caret after a trailing space.
- [ ] Argument mode shows the format provider's completions (today `wait=true|false`).
- [ ] State-only renders without items; the popup never steals focus.
- [ ] Wired into `UserInputView`; the debug page shares the same view model and control.
- [ ] Locale keys for the new strings added to `iv` and `ru-RU`; main + desktop build; verified manually.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
