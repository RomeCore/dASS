# 27: `SlashCommandInputAnalyzer` + highlight provider + theme brushes

Status: open
Type: task
Blocked by:

## What to build

The shared "what is in the box" core and its first consumer — the real highlighting for the chat input.

- **`SlashCommandInputAnalyzer`** — pure, static: given the raw input `text`, the caret index and the chat's commands,
  return `SlashCommandInputAnalysis`: whether the text is a command (leading `/`, with `//` escaping it), the
  `TokenSpan`, the `ArgumentSpan`, the slash-free token, the `ResolutionState`
  (`None | Partial | Unknown | Known | WonOthers`) and whether the caret sits in the token / in the arguments. One
  command per message, **only at the start** — the same rule as `SlashCommandExtractor`. (The debug provider's
  per-line rule is wrong and gets fixed here.) This analyzer is also consumed by ticket 26.
- **`SlashCommandHighlightTransformProvider`** — a thin `IHighlightTransformProvider` (the existing delegate
  `HighlightTransformProvider` is enough) over the analyzer: known token pink, unknown red + underline, `WonOthers`
  orange + underline, argument text light grey; a ghost hint for a required-but-missing argument.
- **Theme brushes** — named brushes in `HighlightTextBoxTheme.axaml` (`SlashCommandKnownBrush`,
  `SlashCommandUnknownBrush`, `SlashCommandArgumentBrush`, `SlashCommandAmbiguousBrush`) with code fallbacks, so the
  palette is themable later.
- Move `HighlightTextBoxDebugPageViewModel` onto the real analyzer/provider.

## Acceptance criteria

- [ ] `SlashCommandInputAnalyzer` is pure and covers: empty/whitespace text, `//` escape, caret in the middle of the
      token, multi-line input (only the leading token counts), unknown, known, `WonOthers`.
- [ ] The provider emits the palette above and a required-arg ghost; the debug page renders it.
- [ ] Brushes are theme resources with code fallbacks.
- [ ] Unit tests for the analyzer (boundary + negative).
- [ ] Main + desktop build; the debug page works in the app.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
