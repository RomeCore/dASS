# Prototype — Highlighting and argument completion

A rough, concrete design artifact for ticket **Highlighting and argument completion**. React to it.

## Span model (grounded)

- `IHighlightTransformProvider.Transform(text)` → `HighlightTransformResult(RenderedText, HighlightSpans)`.
- `HighlightSpans` are `TextHighlightSpan(Start, Length, Brush, Decorations)` in **raw-text coordinates**.
- `RenderedText` must be **isometric (1:1)** or **suffix-only** (append at the end); mid-string edits corrupt the caret/selection mapping.
- ⇒ All colouring is spans over the raw text; **ghost text is a suffix append**, never an in-the-middle insertion.

## What gets coloured (v1)

| Region | Colour |
|---|---|
| `/` + namespace + name, **known** | **pink** |
| `/` + namespace + name, **unknown** | **red, underlined** |
| argument text | **light grey** |
| **ambiguous** command (resolves only by qualifier) | **orange, underlined** (ticket 02) |

## Ghost text

- For a **required-but-missing** argument, append a suffix hint via `RenderedText`, e.g.
  `…/agent:web-searcher ▸ <url>` (the `▸ <url>` part is ghost, non-editable).
- Caveat: suffix-only — ghosting *between* tokens (e.g. completing `wait=`) is not possible; keep hints trailing.
- **Token autocomplete ghost**: while typing the command token (namespaces + name), the matched completion is shown as a ghost suffix; pressing **`→` (Right)** accepts **one character**, turning it from ghost into real typed text. (So the ghost text of a token is a shell-style inline completion, consumed char-by-char.)

## Argument completion

- Delegated to `ISlashCommandArgumentFormatProvider.Complete(prefix, ctx)` (sync `IEnumerable`), only when `CanComplete`.
- **v1 reality**: command/namespace **names are not arguments** (ticket 12) — completing `grilling` in `/skill:grilling` is the *general autocomplete service* (ticket 08). Argument completion exists for:
  - `wait=true|false` on `/agent:<name>` → a two-choice provider;
  - future tool/file arguments → a file-path provider.
- The completion popup is the **same `Popup`** as ticket 08, just fed from a different source.

## Coexistence with the popup

- Highlighting is **pure rendering** (transform provider); the popup is an **overlay** anchored to the caret.
- The transform provider knows **nothing** about the popup; caret/selection stay untouched (the control is not virtual).

## Decided

- palette: pink (known) / red+underline (unknown) / light grey (args) / orange+underline (ambiguous); theme brushes are a later concern;
- ghost text for required args in v1: **yes**;
- argument completion in v1: `wait=true|false` choice provider; more providers (e.g. files) are **added to the `ISlashCommandArgumentFormatProvider` set** — the engine is not touched;
- runnable prototype: **not needed** — this artifact is the deliverable.
