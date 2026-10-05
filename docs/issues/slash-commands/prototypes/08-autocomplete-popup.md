# Prototype — Autocomplete popup

A rough, concrete design artifact for ticket **Autocomplete popup**. React to it; nothing here is code yet.

## Mechanism (grounded in existing code)

- `HighlightTextPresenter.GetCaretRectIn(Visual target)` **already exists** and returns the caret rectangle in the coordinates of a target visual — use it as the popup anchor.
- Use a **`Popup`** (not `Flyout` — flyouts are focus/placement-stealing and are bound to buttons; the codebase uses them for menus). The popup lives in the input control's overlay layer:
  - `PlacementTarget` = the `HighlightTextBox`;
  - `Placement = BottomEdgeAlignedLeft`;
  - `HorizontalOffset` / `VerticalOffset` computed from `GetCaretRectIn(overlayLayer)`;
  - the `HighlightTextBox` **keeps focus** — the popup must not steal it; all keys are handled by the TextBox, the popup is a passive list.
- The list is a lightweight `ItemsControl` bound to the **general autocomplete service** (tickets 02/08) — the popup itself owns no matching logic.

## States

| # | State | Behaviour |
|---|---|---|
| 1 | Closed | No `/` token under the caret |
| 2 | Open (fresh `/`) | Full command list |
| 3 | Filtering (`/gr`) | Filtered list, first row selected |
| 4 | Empty (`/zzz`) | One "no matching commands" row; token still highlighted as unknown |
| 5 | Unknown committed | No popup; token highlighted red |
| 6 | Argument mode (`/skill:grilling ▮`) | Popup switches to argument completion (ticket 09 / `ISlashCommandArgumentFormatProvider`) |

## Keyboard map

| Key | Action |
|---|---|
| `↓` / `↑` | Move selection |
| `Enter` | Accept the selection **if the popup is open**; otherwise send the message |
| `Tab` | Accept the selection |
| `Esc` | Close the popup (input untouched) |
| typing | Filters the list; caret stays in the TextBox |

## Insertion on accept

- Replace the typed `/gr…` fragment with the **resolved, fully-qualified** form (`/skill:grilling`), append a trailing space, keep the caret after it.
- Keep the popup closed until the user starts typing the next argument.

## Row display

- icon = command type (`skill` / `agent` / …);
- name = the qualified form (`/skill:grilling`);
- description (localized);
- **defeated/shadowed commands are marked** (ticket 02) and offered under their qualifier.

## Open questions (to react to)

- visible-row limit / max height / scrolling;
- mouse: hover to select? click to accept? (recommended: yes);
- grouping by type, or a flat list sorted by `Order` then name (decided: flat, `Order` then name — `OverrideOrder` is *only* for resolving same-name conflicts, not a sort key);
- should the popup re-open while typing **arguments**, or is argument completion a separate affordance?
