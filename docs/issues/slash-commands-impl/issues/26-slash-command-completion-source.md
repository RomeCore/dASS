# 26: `SlashCommandCompletionSource`

Status: resolved
Type: task
Blocked by: 25, 27

## What to build

The slash-command `IInputCompletionSource` the popup binds to. It owns two states; the second one delegates.

- **Token state** — the caret is inside (or right after) the leading command token. Prefix-match the typed token
  (case-insensitive) against every command's `Name`, `Aliases` and namespace segments; order by `Order` then name
  (`OverrideOrder` is **not** a sort key); mark defeated commands (`IsDefeated`) and offer them under a qualifier.
  `InsertText` is the **fully-qualified** form (`/` + `CanonicalToken`, e.g. `/skill:grilling`); `Span` covers the
  typed token including the leading `/`; `State` names the command state.
- **Argument state** — the token resolved and the caret sits in the argument region: find the argument under the caret
  through `SlashCommandArgumentParser.Parse(schema, rawArguments)` (its `SlashCommandRawArgument.Position` /
  `ValuePosition` / `ValueLength` give the span) and, when the slot's `Format is { CanComplete: true }`, delegate to
  `Format.Complete(prefix, ctx)`. **No `wait`-specific code**: any `ISlashCommandArgumentFormatProvider` with
  `CanComplete` participates — `SlashCommandBooleanFormatProvider` is merely the only one today. `Span` is the current
  argument value; `State` names the argument.

The prefix matching and the "argument under the caret" lookup are extracted as pure, testable helpers; the source
itself is registered as a chat-scoped `IInputCompletionSource`.

## Acceptance criteria

- [x] `/gr`, `/skill:`, `/matt:gr` produce the expected candidates; a bare-name defeated command is `IsDefeated` and
      its `InsertText` is qualified.
- [x] `InsertText` is `/` + `CanonicalToken`; `Span` covers the typed token.
- [x] In argument mode the completions come from the slot's `ISlashCommandArgumentFormatProvider.Complete`; a slot with
      no provider (or `CanComplete == false`) yields no items but a non-null state.
- [x] Registering another format provider with `CanComplete` makes it complete without changing this ticket's code.
- [x] Pure matcher/lookup unit tests (boundary + negative); filtered slash-commands suite green.
- [x] Main builds.

## Answer

`SlashCommandCompletionSource` — a chat-scoped `IInputCompletionSource` (`Priority = 100`) over
`IAddonSetCollector<SlashCommandInfo>.GetAddonsForChat()`:

- **Token state** (`analysis.IsCaretInToken`): `SlashCommandPrefixMatcher.Match` over the typed token, ordered by
  `Order` then name (then key), each item `InsertText = "/" + CanonicalToken`, `Description = DescriptionKey`,
  `Kind = Command`, `IsDefeated` for the same-named losers; `Span = analysis.TokenSpan`; `State = Command`. An empty
  match set still returns a result (the popup shows "no matches").
- **Argument state** (`analysis.IsCaretInArguments`, needs a resolved command): the new pure
  **`SlashCommandArgumentLookup`** locates the argument under the caret in `SlashCommandArgumentParser.Parse(...)`'s
  result (its value region + the prefix typed before the caret) and, when the slot's `Format` declares `CanComplete`,
  delegates to `ISlashCommandArgumentFormatProvider.Complete`. `Span` is the value region. No `wait`-specific code —
  adding a provider with `CanComplete` makes it complete without touching this class.
- Returns `false` when the caret is not in the token/argument region, when there are no commands, when the text is not
  a command, when the resolved command has no completable argument, or when the caret sits in the rest positional / on
  a key (neither declares a completable slot).

**Ghost:** `GhostText` is left `null` here. The v1 commands declare no *required* argument (skills and sub-agents
declare only a rest positional), so there is no required-argument hint to produce; the token inline-completion ghost's
placement and acceptance are the control's (tickets 28/29). The renderer already draws `GhostText` and is covered by
its own tests.

Tests: source **11**, lookup **4** (15 new; 64 green in the area). Main builds.

## Comments

<!-- appended conversation -->
