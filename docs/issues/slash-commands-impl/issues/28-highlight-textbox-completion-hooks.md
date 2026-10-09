# 28: `HighlightTextBox` completion hooks

Status: open
Type: task
Blocked by: 25

## What to build

The generic, command-agnostic control-side plumbing the popup and the ghost need from `HighlightTextBox`. It knows only
`InputCompletionResult` — it never learns what a slash command is.

- **Caret accessor** — a public `GetCaretRect(Visual target)` (delegating to the existing
  `HighlightTextPresenter.GetCaretRectIn`) and a lightweight caret/selection-changed notification, so the owning view
  model can re-anchor and re-filter without polling.
- **Input hook** — a `TryHandleKey`-style hook (or an overridable method) the owning view calls **first** in its tunnel
  `KeyDown`, so an open popup can consume `↑/↓/Tab/Enter/Esc` before the TextBox acts.
- **Ghost rendering** — `RenderedText` with the ghost text inserted **at the caret**. Mid-string insertion is allowed:
  the presenter renders it; only the caret/selection stay clamped to the real `Text`.
- **`→` (Right) accept** — replace one character of the real token tail `[caret..spanEnd)` with one character of the
  ghost; when the real tail is exhausted (or the caret is at the token end) just insert the ghost character.
- **Reset on manual caret moves** — any pointer-driven caret/selection change clears the completion state
  (popup + ghost) and stops calling the completion service, so the caret clamp never gets in the way.

## Acceptance criteria

- [ ] `GetCaretRect(Visual)` is public and returns the caret rect in the target's coordinates.
- [ ] The owning view can intercept keys before the TextBox; a consumed key does not reach the base behaviour.
- [ ] Ghost text renders at the caret, mid-token included.
- [ ] `→` consumes one real char and commits one ghost char; at the token end it only inserts.
- [ ] A pointer-driven caret/selection change resets the completion state.
- [ ] Main + desktop build; verified manually in the debug page.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
