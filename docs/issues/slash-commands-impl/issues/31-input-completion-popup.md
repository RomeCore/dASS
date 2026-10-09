# 31: Input completion popup rework

Status: resolved
Type: task
Blocked by: 30

## What to build

Stage 5's input UI was build-green but had never been run. The first live run
(`/agent:%LOCALAPPDATA%:web-searcher Погода Москва wait=true`) exposed four problems and two follow-ups; the design
decisions behind the fixes were settled in a grilling session and are recorded in the plan's
`## Committed decisions (input UX fixes)`.

1. **The popup's chrome** was Fluent, not the app's (`SolidBackgroundFillColorSecondaryBrush` /
   `CardStrokeColorDefaultBrush` — brushes the app's palette does not even define).
2. **Argument completion looked wrong**: the ghost was inserted at the caret beside the tail it was previewing, so
   `wait=tru` drew `truee`.
3. **The state was never filled**: `InputCompletionState` carried `Title` / `Description` / `Kind`, and only `Kind` was
   written.
4. **The command's context was never shown**: the popup listed candidates and nothing else — no command name, no
   description, no arguments.
5. The token name had **no inline ghost** (`→` worked for arguments only).
6. Canonical names were **namespace-heavy** (`agent:%LOCALAPPDATA%:web-searcher`).

## Acceptance criteria

- [x] The popup shows the command's context: its name and description, the argument under the caret (the rest
      positional included) and the declared arguments with a "required" mark.
- [x] The picker stays a single list (one source claims a caret) and is a plain stack, not a `ListBox`; a click accepts
      the row it landed on.
- [x] The popup sits above the input, anchored horizontally to the caret and clamped into the window, with its own
      palette (`Styles/Theme/InputCompletionTheme.axaml`) and a footer with the keys it owns.
- [x] The popup opens whenever the source yields a state, including while the free text of `/agent:<name>` is typed.
- [x] The ghost replaces the region's tail instead of being inserted, for arguments and for the token name alike.
- [x] Every argument is a chipped unit: keyed name green, `=` darker green, value grey, grouping quotes lighter.
- [x] A command is offered under the shortest token that still resolves to it, with the fully-qualified form on hover.
- [x] `Ctrl+Enter` sends without a generation intent.
- [x] A completion source renders its own regions, and the input view knows nothing about commands.
- [ ] **Manual verification in the app is pending** (see below) — the UI parts are build-green and unit-tested where
      pure, but nothing in this ticket was exercised live.

## Answer

Commits: `e5e7190`, `93600d6`, `72074ad`, `c7f3e87`, `e2040be`, `33a8530`, `bf16666` (the maintainer's own tree edits
went in first, as `8e6d0c1`).

- **State model.** `InputCompletionState` gained `ContextTitle` / `ContextDescription` / `ContextItems`, and
  `InputCompletionContextItem` is the informational row (name, description, `IsRequired`, `IsCurrent`) — it is never
  selected or accepted. The picker stays exactly one (`Result.Items`, `SelectedIndex`, `GhostText`), because a single
  source claims a caret; the token mode names the picker `command.completion.title.commands`, the argument mode shows
  the command as the header, the argument under the caret as the context heading and the declared arguments as its rows
  (the current-argument block and the list are shown *together*, flip-able with the
  `ShowArgumentListWithCurrentArgument` constant in the source). Filled values are not shown and defaults are not
  substituted; `no_matches` renders in the token mode only (`ShowNoMatches`).
- **Popup.** `ListBox` → a stack: state icon (`InputCompletionIcons` maps a kind to a `VisualIconKind`), header, context
  block, picker (`InputCompletionRow` carries its own `IsSelected`, since there is no list control to own the
  selection) and a footer with the keys. Only the picker accepts, and it accepts the row the click landed on. The
  palette lives in `Styles/Theme/InputCompletionTheme.axaml`, merged into the theme resources like the `HighlightTextBox`
  palette, so a theme overrides it without touching the markup.
- **Geometry.** `Placement="TopEdgeAlignedLeft"` (above the target, left edges aligned — verified against Avalonia's
  `PlacementMode` docs) plus a horizontal offset to the caret, applied on the next layout pass (the popup's width is
  known only after it measured) and clamped into the window while accounting for the input's own position.
- **Ghost.** The renderer draws `text[..caret] + ghost + text[Span.End..]`, so a preview that continues the typed text
  is not drawn beside the real tail; `ghost = InsertText[prefix.Length..]` only while the completion continues the raw
  prefix before the caret, with an empty prefix allowed for a keyed value but not for a bare `/`. The token state gets a
  ghost too. The presenter's "mid-string transforms are unsafe" remark was stale and is rewritten.
- **Argument palette.** `TextHighlightSpan` gained an optional `Background`, passed through to the run's
  `backgroundBrush`. A part carries *both* its colour and the chip because a text run has a single property set and (per
  Avalonia's `FormattedTextSource`) the first span covering a position wins — a chip drawn as a separate overlapping
  span is not possible. The parse the renderer needs now comes from `SlashCommandInputAnalyzer`
  (`SlashCommandInputAnalysis.Arguments`, via `TryParse`) and the completion source reuses it, which also removed an
  exception path: `SlashCommandArgumentParser.Parse` throws on an unterminated quote and the source ran on every
  keystroke. An argument list that does not parse falls back to the plain argument colour.
- **Rendering belongs to the source** (the maintainer's call, and right): `IInputCompletionRenderer` is implemented by
  `SlashCommandCompletionSource` next to `IInputCompletionSource`, the separate `SlashCommandHighlightTransformProvider`
  is gone, and `InputCompletionTransformProvider` (chat-scoped) is the single thing the input binds to — it
  concatenates the regions and draws the ghost of the current `InputCompletionResult`. `UserInputViewModel` resolves
  one interface and no longer mentions commands, `IInputCompletionService` became a session (`Update` / `Close` /
  `Result` / `ResultChanged`), and `IHighlightTransformProvider.Transform` takes the caret (the presenter owns it), so
  the caret delegates are gone. The ghost is now a projection of the completion, which is why `Escape` and a
  pointer-driven caret move take it away too (and the provider raises `LayoutChanged` on a result change — previously
  `NotifyLayoutChanged` was never called, so a closed completion kept its ghost).
- **Short tokens.** `SlashCommandShortToken.For` is the shortest suffix of the canonical token that the resolver still
  resolves to that very command, so the offered form equals what typing it by hand would pick; it is used for
  `InsertText` and for the command's name in the context block, with the fully-qualified token as the item's `Hint` on
  hover. Aliases are never inserted, and a defeated command keeps the qualifier that tells it apart. No memoisation: the
  set is the chat's commands and the strings are short, while a change signature would allocate as much as it saves.
  The ghost follows the offered form, so typing the qualified form previews nothing inline.
- **`Ctrl+Enter`** sends with the toolbar Send semantics (no generation intent) and, being checked before the popup,
  neither accepts an item nor closes the popup; while a generation runs both Enter keys cancel it. The Send button's
  tooltip names the shortcut, and the generate button got its own hint key.
- **Deviation worth naming**: the `ghost` colour left `SlashCommandHighlightPalette` for the generic
  `InputCompletionGhostBrush`, because the input draws the ghost, not the command layer. The palette's
  `FromResources()` is actually called now — it never was, so every theme override of it had been dead.

Verified: **204** slash-command tests (excluding the pre-existing `ChatMessageInsertionServiceTests` host deadlock),
**34** `InputCompletion` tests and those **24** in isolation; main and desktop builds green.

## Manual verification checklist (pending)

`/` → the "Commands" header and collapsed names (full token on hover, the loser keeping its qualifier); `/gri` → the
inline ghost; `/agent:web-searcher ` → the popup above the input, at the caret, with the command's context and
"сообщение" as the current argument; `wait=` → the argument block and `true` / `false`, the chips with their colours;
`→` on `wait=tru` → no doubled tail; `Esc` → the popup *and* the ghost go; `Ctrl+Enter` → a send without generation;
long text near the right edge → the popup stays inside the window.

## Comments

<!-- appended conversation -->
