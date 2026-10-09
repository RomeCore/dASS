# 30: The rest positional as a schema slot

Status: resolved
Type: task
Blocked by: none

## What to build

The rest positional existed only as a flag (`HasRestPositional`): the grammar stopped splitting at the first surplus
token and delivered the tail as a bare string with no span, so nothing downstream could tell **which** argument the
caret is inside when the user is writing free text. The input-completion popup (ticket 31) needs exactly that: the
"message" of `/agent:<name>` is an argument with a name, a description and a span, like any other.

- **`SlashCommandArgumentSchema`**: `HasRestPositional` (an `init` bool) → `RestPositional`
  (`SlashCommandArgument?`, `init`), with `HasRestPositional` kept as a computed convenience
  (`RestPositional is not null`) so no caller changes shape.
- **The slot is descriptive**: the rest is never split, never reported missing, never validated and never converted —
  `Required`, `Default` and `Format` on it do not apply. Documented on the property.
- **`SlashCommandParsedArguments`** gains `RestPositional` (`SlashCommandRawArgument?`, with the surplus span), and
  `RestPositionalArguments` becomes a projection of it (`RestPositional?.Raw ?? string.Empty`) so there is one source of
  truth. It stays **out of** `Positionals`, so the binder's positional-to-schema alignment is untouched.
- **The parser** builds that raw argument (`BuildRestPositional`): span = the first surplus token to the end of the
  positional text, trailing whitespace excluded, taken verbatim (quotes kept) — `null` when the schema has no rest slot
  or the surplus is empty.
- **`SlashCommandArgumentLookup`** searches the rest last: a caret anywhere in the free text now resolves to it.
- **The two executors** name the slot: `SkillCommandExecutor` → `command.argument.skill.arguments` (+ `.description`),
  `SubAgentCommandExecutor` → `command.argument.agent.message` (+ `.description`), both in `iv` and `ru-RU`.

No user-visible behaviour on its own: the popup still ignores the state, and rendering it is ticket 31.

## Acceptance criteria

- [x] `HasRestPositional` is computed from `RestPositional`; no `init` setter remains.
- [x] The parsed rest carries its `Definition` (the schema slot) and a span (`Position` / `Length` / `ValuePosition`).
- [x] `RestPositionalArguments` is derived from the raw argument.
- [x] A caret inside the free text resolves to the rest slot through `SlashCommandArgumentLookup`.
- [x] `/skill` and `/agent` declare the slot with a localizable name and description (`iv` + `ru-RU`).
- [x] Slash-command suite green (the pre-existing `ChatMessageInsertionServiceTests` host deadlock is worked around as
      usual, in two passes).

## Answer

- `SlashCommandArgumentSchema.RestPositional` + computed `HasRestPositional`; the descriptive contract sits in its
  `<remarks>`.
- `SlashCommandParsedArguments.RestPositional` (`SlashCommandRawArgument?`) with
  `RestPositionalArguments => RestPositional?.Raw ?? string.Empty`. The **binder needed no change**: it copies the
  projected string, and because the rest never enters `Positionals` the "too many arguments" guard and the positional
  alignment stay exactly as they were.
- `SlashCommandArgumentParser.BuildRestPositional` (private) emits the raw argument; `TryParse` passes it into both
  result branches — the error branch included, so the binder still carries the verbatim text after a parse failure.
- `SlashCommandArgumentLookup.Find` asks for the rest last, after the declared positionals and the keyed arguments: the
  rest spans the whole surplus region, so anything more specific has already won by then.
- The executors' schemas: a rest slot carrying `command.argument.skill.arguments` and `command.argument.agent.message`.
- Tests: grammar (the span, and `null` without a surplus), lookup (the rest target and its typed prefix), the
  binder/insertion/fingerprint schemas migrated to the slot, and the completion source's rest case flipped from
  "returns false" to "a state-only result" — the caret in the free text is now accounted for, and the popup's content
  for it is ticket 31.

Verified: **195** slash-command tests green (excluding `ChatMessageInsertionServiceTests`) plus those **24** green in
isolation; main build green.

## Comments

<!-- appended conversation -->
