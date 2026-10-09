# 26: `SlashCommandCompletionSource`

Status: open
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

- [ ] `/gr`, `/skill:`, `/matt:gr` produce the expected candidates; a bare-name defeated command is `IsDefeated` and
      its `InsertText` is qualified.
- [ ] `InsertText` is `/` + `CanonicalToken`; `Span` covers the typed token.
- [ ] In argument mode the completions come from the slot's `ISlashCommandArgumentFormatProvider.Complete`; a slot with
      no provider (or `CanComplete == false`) yields no items but a non-null state.
- [ ] Registering another format provider with `CanComplete` makes it complete without changing this ticket's code.
- [ ] Pure matcher/lookup unit tests (boundary + negative); filtered slash-commands suite green.
- [ ] Main builds.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
