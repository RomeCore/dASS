# Autocomplete popup

Status: resolved
Type: prototype
Blocked by: 02

## Question

Prototype the **autocomplete popup** over `HighlightTextBox`.

Cover:

- trigger: `/` at the start of the input;
- the popup list of commands (name, namespace, description);
- fuzzy filtering, keyboard navigation (Up/Down/Enter/Tab/Esc);
- insertion behaviour; empty / unknown states;
- the Avalonia mechanism: a `Popup`/`Flyout` anchored to the caret (via `HighlightTextPresenter`), and whether the existing `IHighlightTransformProvider` foundation suffices or extra control work is needed.

Deliver a cheap, runnable prototype (e.g. extend `HighlightTextBoxDebugPage`) to react to.

## Prototype

- [Prototype — Autocomplete popup](../prototypes/08-autocomplete-popup.md) — asset: `docs/issues/slash-commands/prototypes/08-autocomplete-popup.md` (mechanism grounded on `HighlightTextPresenter.GetCaretRectIn`, states, keyboard map, insertion, row display, open questions).

## Answer

### Mechanism

- A **`Popup`** (not `Flyout` — flyouts in this codebase are button-bound and focus-stealing): `PlacementTarget` = the `HighlightTextBox`, `Placement = BottomEdgeAlignedLeft`, offsets computed from `HighlightTextPresenter.GetCaretRectIn(overlay layer)`.
- The `HighlightTextBox` **keeps focus**; the popup is a passive list and handles no input itself.
- The list is bound to the **general autocomplete service** (ticket 02); the popup carries no matching logic.

### States

Closed → Open (fresh `/`) → Filtering → Empty (no matches) → Unknown committed (red, no popup) → Argument mode (argument completion, ticket 09 / `ISlashCommandArgumentFormatProvider`).

### Keyboard

`↓`/`↑` — move selection; `Enter` — accept the selection **if the popup is open**, otherwise send the message; `Tab` — accept; `Esc` — close (input untouched); typing filters.

### Insertion on accept

- Replace the typed fragment with the **fully-qualified** form (`/skill:grilling`), append a trailing space, keep the caret after it; the popup stays closed until the next argument.

### List

- **Sort by `Order`, then name.** `OverrideOrder` is **not** a sort key — it only resolves same-name conflicts (ticket 02).
- Defeated/shadowed commands are marked and offered under their qualifier.
- ~8 visible rows with scroll; mouse hover selects, click accepts; flat list (grouping by type is fog).

### Runnable prototype

- **Not needed** — the design artifact suffices. The map ends at a spec, and a code stand would add little.

## Comments

- Prototype asset created and reacted to; single round. Sort correction: `Order` then name.
