# Highlighting and argument completion

Status: resolved
Type: prototype
Blocked by: 02

## Question

Prototype slash-command **highlighting** and **argument-level completion** in the input box using `IHighlightTransformProvider`.

Cover:

- colouring the command token (known vs unknown), namespaces, and arguments;
- ghost-text hints for required arguments (the `HighlightTransformResult` rendered-text / suffix rule);
- what is typed for v1 argument completion: `/skill:` and `/agent:` name completion, and how free-form arguments are treated;
- the span model and how it coexists with the popup from ticket 08.

## Prototype

- [Prototype — Highlighting and argument completion](../prototypes/09-highlighting-and-argument-completion.md) — asset: `docs/issues/slash-commands/prototypes/09-highlighting-and-argument-completion.md`.

## Answer

### Span model

- All colouring is `TextHighlightSpan`s in **raw-text coordinates**; `RenderedText` is used only **suffix-only** (ghost text) — mid-string renders would corrupt the caret mapping in `HighlightTextPresenter`.
- The transform provider is **pure rendering**; it knows nothing about the popup (ticket 08), which is a separate caret-anchored overlay.

### Palette

| Region | Colour |
|---|---|
| known command (`/` + namespaces + name) | **pink** |
| unknown command | **red + underline** |
| argument text | **light grey** |
| ambiguous command (resolvable only by qualifier — ticket 02) | **orange + underline** |

- Theme brushes are a later concern; the prototype uses these colours.
- Terminology note: the user's preferred UI word for the qualifier-only state is **"ambiguous"**, though the resolver's `Ambiguous` (ticket 02) is a *different* condition (a true unresolvable tie). The display colour/underline is what matters.

### Ghost text

- A **required-but-missing** argument shows a trailing ghost hint (e.g. `…/agent:web-searcher ▸ <url>`) via `RenderedText` — **yes in v1**.
- **Token autocomplete ghost**: while typing the command token, the matching completion appears as a ghost suffix; pressing **`→` (Right)** accepts **one character** at a time, turning it from ghost into real typed text (shell-style inline completion).

### Argument completion

- Delegated to `ISlashCommandArgumentFormatProvider.Complete(prefix, ctx)` (sync `IEnumerable`), only when `CanComplete`.
- Command / namespace **names are not arguments** — completing `grilling` in `/skill:grilling` is the general autocomplete service (ticket 08).
- **v1** argument completion = the `wait=true|false` choice provider on `/agent:<name>`. Additional providers (files, etc.) are added by **extending the `ISlashCommandArgumentFormatProvider` set** — the engine is never touched.

### Runnable prototype

- **Not needed** — the design artifact is the deliverable.

## Comments

- Single prototype round. Palette replaced (pink / red+underline / light grey / orange+underline); ghost-text `→` char-accept added; term debate "shadowed" → "ambiguous".
