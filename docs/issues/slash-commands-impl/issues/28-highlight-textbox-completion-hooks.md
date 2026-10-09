# 28: `HighlightTextBox` completion hooks

Status: resolved
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

- [x] `GetCaretRect(Visual)` is public and returns the caret rect in the target's coordinates.
- [x] The owning view can intercept keys before the TextBox; a consumed key does not reach the base behaviour.
- [x] Ghost text renders at the caret, mid-token included.
- [x] `→` consumes one real char and commits one ghost char; at the token end it only inserts.
- [x] A pointer-driven caret/selection change is signalled (the view model resets the completion on it).
- [ ] Main + desktop build green; manual verification in the debug page pending.

## Answer

- **`HighlightTextBox.GetCaretRect(Visual target)`** — public, delegates to `HighlightTextPresenter.GetCaretRectIn`.
- **`CaretStateChanged`** (raised from the presenter's `CaretIndex` / `SelectionStart` / `SelectionEnd` changes) and
  **`PointerCaretStateChanged`** (raised after a click or a left-button drag). The control only *signals* a
  pointer-driven change; the view model resets the completion on it (ticket 29) — the control stays
  completion-agnostic.
- **`PreviewKeyDown`** — raised at the start of `OnKeyDown`; a handler that sets `Handled` consumes the key before any
  base behaviour.
- **Inline completion (generic):** the new `IInlineCompletionProvider` (`CompletionText`, `TokenEnd`) exposes a ghost
  to the control; on `Right` the control commits one character through the pure **`InlineCompletionAcceptor`**
  (replace one character in `[caret, TokenEnd)` with one completion character; nothing is consumed at/after the token
  end) and parks the caret.
- **The command renderer now implements `IInlineCompletionProvider`** and takes a caret accessor, so it inserts the
  ghost **at the caret** (mid-string included) and exposes the text + token end for `→`. The debug page wires the
  caret to the end of the text.

Verified: main + desktop builds; `InlineCompletionAcceptorTests` (5) and the renderer's mid-caret test; 69 green in
the area. **Manual verification in the debug page is still pending.**

## Comments

- The `→` mechanics live in the control, but the completion *data* still comes from the renderer (it hands over the
  ghost text and the token end), so the control never learns what a slash command is.

<!-- appended conversation -->
