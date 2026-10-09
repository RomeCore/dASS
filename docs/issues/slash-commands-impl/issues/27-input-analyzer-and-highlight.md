# 27: `SlashCommandInputAnalyzer` + highlight provider + theme brushes

Status: resolved
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

- [x] `SlashCommandInputAnalyzer` is pure and covers: empty/whitespace text, `//` escape, caret in the middle of the
      token, multi-line input (only the leading token counts), unknown, known, `WonOthers`.
- [x] The provider emits the palette above; the debug page renders it.
- [x] Brushes are theme resources with code fallbacks.
- [x] Unit tests for the analyzer (boundary + negative) and the renderer.
- [x] Main + desktop build.

## Answer

Delivered in two commits (pure core, then the renderer).

Pure core (`SlashCommands/Input/`):

- **`SlashCommandInputAnalyzer.Analyze(text, caretIndex, commands)`** — pure and static. Returns
  `SlashCommandInputAnalysis`: `IsCommand` (leading `/`, `//` escapes), the slash-free `Token`, `TokenSpan` (the marker
  included) and `ArgumentSpan`, the caret flags `IsCaretInToken` / `IsCaretInArguments`, the resolved `Command`, and
  `ResolutionState`.
- **`ResolutionState` is caret-independent** (so the highlight transform — which receives the text but not the caret —
  can colour the token): `Partial` = the token is still a prefix of some command (or empty); otherwise exact
  resolution gives `Unknown` / `Known` / `WonOthers`. The caret flags are for the popup.
- **`SlashCommandPrefixMatcher`** — the namespace-aware "could this still become a command?" matcher (last segment
  prefixes the name/an alias; the earlier segments prefix distinct namespaces, any order); reused by ticket 26.

Renderer (`SlashCommandHighlightTransformProvider`):

- **A pure projection of the given `InputCompletionResult`** — it does not fetch the completion itself (the agreed
  architecture: data ↔ UI). The view model supplies the current result and the commands through delegates.
- Paints the token by state (pink / red+underline / orange+underline), greys the arguments, and appends the result's
  `GhostText` as a ghost span. With no commands it paints nothing (commands disabled / empty set).
- **`SlashCommandHighlightPalette`** — code defaults plus `FromResources()`, which reads the named theme brushes
  (`SlashCommandKnownBrush` / `Unknown` / `Ambiguous` / `Argument` / `Ghost` in `HighlightTextBoxTheme.axaml`) and
  falls back per missing brush.
- The debug page now runs on the real analyzer + renderer (its old per-line rule — every line start a command — is
  gone; only the leading token is a command). The old `EnableTransform` angle-quote demo was dropped.

**Ghost scope (agreed):** the renderer *draws* `InputCompletionResult.GhostText`; the *text* of the required-arg hint
is produced by the completion source (ticket 26). The debug page demos the ghost with a stand-in completion.

Tests: analyzer **13**, prefix matcher **17**, renderer **19** (49 in the area, green). Main + desktop builds green.

## Comments

<!-- appended conversation -->
