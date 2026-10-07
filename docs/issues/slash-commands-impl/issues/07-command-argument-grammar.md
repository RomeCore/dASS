# 07: Command argument grammar and schema

Status: claimed
Type: task
Blocked by:

## What to build

The argument layer of the command engine: the schema model, the raw/parsed argument model, the format-provider contract,
the argument parser and the binder that applies defaults, validates and converts.

This ticket comes **first** in Stage 1: the executor context (ticket 08) carries the parse result, and
`ISlashCommandExecutor` declares its argument schema through these types.

### Ownership split (agreed with the maintainer)

- **agent** — every type below, the `SlashCommandArgumentBinder` implementation, the RCParsing **scaffold**
  (`SlashCommandArgumentParser`: public shape + placeholder grammar) and the full test suite for the grammar contract.
- **user** — the actual RCParsing grammar inside `SlashCommandArgumentParser` (the maintainer authors RCParsing).
  The ticket resolves only when the grammar is in and the (un-skipped) tests are green.

Files live under `src/LLMDesktopAssistant/SlashCommands/Arguments/`.

### Schema

```csharp
public class SlashCommandArgumentSchema
{
    public ImmutableList<SlashCommandArgument> Positionals { get; init; } = [];
    public ImmutableDictionary<string, SlashCommandArgument> Keyed { get; init; } = [];
    public bool HasRestPositional { get; init; }   // the "big" argument: the surplus beyond the positionals is delivered raw
}

public class SlashCommandArgument
{
    public required LocaleKeyBase Name { get; init; }
    public LocaleKeyBase? Description { get; init; }
    public bool Required { get; init; }
    public string? Default { get; init; }
    public ISlashCommandArgumentFormatProvider? Format { get; init; }
}
```

No `ValueType`, no `Choices`.

**Everything `init`-only, deliberately.** The schema is semantically atomic — it travels with the command definition
and never changes while a command lives; a command is (re)defined by re-parsing its file or re-synthesising it in a
provider, never by mutating an existing schema (the design ticket's `{ get; set; }` is widened by mistake).
`ImmutableList<T>` / `ImmutableDictionary<,>` are only *shallowly* immutable, so the leaf `SlashCommandArgument` is
`init`-only too — otherwise `schema.Keyed["x"].Default = …` would still leak into a shared schema.
The addon's **reference** to the schema (`SlashCommandInfo.ArgumentSchema`) does stay settable — see ticket 08.

### Format-provider contract

```csharp
public interface ISlashCommandArgumentFormatProvider
{
    bool TryValidate(string raw, out LocaleKeyBase? error);   // error is a locale key
    object? Convert(string raw);
    bool CanComplete { get; }
    IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx);
}

public sealed class SlashCommandCompletionItem
{
    public required string Value { get; init; }
    public string? Display { get; init; }
    public LocaleKeyBase? Description { get; init; }
}

public sealed class SlashCommandCompletionContext
{
    public required string RawArguments { get; init; }
    public required string CurrentPrefix { get; init; }
}
```

`SlashCommandCompletionContext` deliberately does **not** reference `SlashCommandInfo` (it is defined in ticket 08, after
this one); ticket 08 extends it with `Command`. Nothing consumes completion in Stage 1 — the only real provider (the
`wait=true|false` choice) arrives with the `/agent` command (Stage 3) and its UI wiring is Stage 5.

### Raw parser output (the grammar's product)

```csharp
public sealed class SlashCommandArgumentsResult
{
    public required string RawArguments { get; init; }               // the whole remainder after the token
    public required string RawPositionalArguments { get; init; }     // every positional argument, verbatim (quotes kept)
    public required string RestPositionalArguments { get; init; }    // the surplus beyond the declared positionals (only when HasRestPositional)
    public required ImmutableList<SlashCommandRawArgument> Positionals { get; init; }
    public required ImmutableDictionary<string, SlashCommandRawArgument> Keyed { get; init; }
    public int ErrorPosition { get; init; } = -1;                    // offset into RawArguments, -1 = no error
    public LocaleKeyBase? Error { get; init; }                       // syntax error (unterminated quote, ...)
}

public sealed class SlashCommandRawArgument
{
    public SlashCommandArgument? Definition { get; init; }           // schema slot; null = surplus positional
    public required string Raw { get; init; }                        // the value as written: grouping quotes present
    public required string Unescaped { get; init; }                  // grouping quotes stripped, `\"` unescaped
    public required bool WasQuoted { get; init; }                    // the value was written as a quoted group
    public required int Position { get; init; }                      // offset in RawArguments: the token / the whole key=value
    public required int Length { get; init; }                        // length of that whole span
    public required int ValuePosition { get; init; }                 // the value's offset: == Position for a positional, past '=' for a key
    public required int KeyLength { get; init; }                     // the key identifier's length; 0 for a positional
    public int ValueLength => Position + Length - ValuePosition;     // always == Raw.Length
}
```

### Parser (scaffold; the grammar is user-owned)

```csharp
public static class SlashCommandArgumentParser
{
    public static Parser Parser { get; }   // RCParsing grammar, built in the static ctor

    public static SlashCommandArgumentsResult Parse(SlashCommandArgumentSchema schema, string rawArguments);
    public static bool TryParse(SlashCommandArgumentSchema schema, string rawArguments,
        out SlashCommandArgumentsResult result, out LocaleKeyBase? error);
}
```

The schema reaches the grammar through an RCParsing metadata factory — the same shape the LLT parser already uses
(`parser.Parse(input, [factory])`). The agent ships the class with a documented placeholder grammar
(`throw new NotImplementedException("RCParsing grammar — maintainer-owned")` behind a `TODO`); the grammar itself is
written by the maintainer.

Grammar contract (from the design tickets 01/12):

- whitespace separates; `'…'` / `"…"` group and the quotes are **stripped**; `\` escapes `"` inside double quotes;
- every token carries a "was quoted" flag;
- a single left-to-right resolver: `key=…` where `key` is declared in the schema's `Keyed` set starts a **keyed
  argument**; an **undeclared** `key=value` stays plain text; everything else is a **positional**;
- an **unquoted** keyed value runs to the next **declared** key (spaces allowed); a **quoted** value bounds it;
- **mixed** is allowed — positionals precede the first key;
- every raw argument carries its **source span** in `RawArguments` — `Position`/`Length` for the whole occurrence
  (the token, or the whole `key=value`), `ValuePosition`/`KeyLength` for the value and the key inside it, with
  `ValueLength` computed (`== Raw.Length`) — so a consumer can locate an argument in the text;
- every positional argument is kept **verbatim** (quotes and spacing kept) as `RawPositionalArguments` — the whole
  region before the first key;
- `HasRestPositional`: the declared positionals are parsed as usual and the **surplus** beyond them is **not** split
  further; declared `key=…` segments are still extracted and that surplus is delivered **verbatim** (quotes kept) as
  `RestPositionalArguments` — a slice of `RawPositionalArguments`, identical to it when no positionals are declared.
  That surplus is what the `/agent` command forwards into `AgentUserMessage.Content`.

### Binder (fully implemented here)

```csharp
public sealed class ParsedSlashCommandArgument
{
    public required SlashCommandArgument Definition { get; init; }
    public SlashCommandRawArgument? Raw { get; init; }               // the user's argument (span, quotes); null = a default or absent
    public string? Ready => Raw?.Unescaped ?? Definition.Default;    // the text the value was made of
    public object? Value { get; init; }                             // Format is null ? Ready : Format.Convert(Ready)
}

public sealed class SlashCommandBoundArguments
{
    public required ImmutableList<ParsedSlashCommandArgument> Positionals { get; init; }
    public required ImmutableDictionary<string, ParsedSlashCommandArgument> Keyed { get; init; }
    public required string RawPositionalArguments { get; init; }     // every positional argument, verbatim
    public required string RestPositionalArguments { get; init; }    // the rest positional, verbatim; empty when the schema declares none
    public LocaleKeyBase? Error { get; init; }
    public int ErrorPosition { get; init; } = -1;                    // carried over on a syntax error; -1 for binding errors
}

public static class SlashCommandArgumentBinder
{
    public static SlashCommandBoundArguments Bind(
        SlashCommandArgumentSchema schema, SlashCommandArgumentsResult parsed);
}
```

`Bind` rules, in order:

1. a parser syntax error is propagated as the result error together with its `ErrorPosition`; the two verbatim fields
   (`RawPositionalArguments`, `RestPositionalArguments`) are carried over untouched — never validated, never converted;
2. positionals by index — absent with `Required` and no `Default` → `command.error.missing_argument`;
   surplus positionals (beyond the schema) → `command.error.too_many_arguments`;
3. keyed — for each **declared** key: present → its raw value; absent with `Default` → the default; absent with
   `Required` and no `Default` → `command.error.missing_argument`;
4. `Format.TryValidate(raw, out error)` for every **user-supplied** value (`Default`s are authored and trusted) —
   a failure yields that error and **blocks the send** (ticket 05);
5. `Value = Format is null ? Ready : Format.Convert(Ready)` — conversion runs only after validation passed.

`ErrorPosition` is `-1` for every error the binder itself raises — a missing or invalid argument has no offset into the
text.

## Acceptance criteria

- [ ] `SlashCommandArgumentSchema`, `SlashCommandArgument`, `ParsedSlashCommandArgument`, `SlashCommandRawArgument`,
      `SlashCommandArgumentsResult`, `SlashCommandBoundArguments`, `ISlashCommandArgumentFormatProvider`,
      `SlashCommandCompletionItem`, `SlashCommandCompletionContext` exist under `SlashCommands/Arguments/`.
- [ ] `SlashCommandArgumentParser` exists with the public shape above; `Parse`/`TryParse` route through the RCParsing
      `Parser` and the schema metadata factory.
- [ ] `SlashCommandArgumentBinder.Bind` is implemented and unit-tested for all five rules above.
- [ ] The grammar contract is covered by tests (whitespace, quote stripping, `\"`, undeclared `key=`, unquoted value
      running to the next declared key, quoted value bounding, mixed positional-before-key, every positional verbatim as
      `RawPositionalArguments`, rest-positional surplus verbatim with declared keys extracted and slicing
      `RestPositionalArguments`). While the grammar is pending these are `Skip`ped with
      `Skip = "RCParsing grammar pending (maintainer)"` so `main` stays green; the ticket resolves only after they are
      un-skipped and green.
- [ ] Locale keys `command.error.missing_argument`, `command.error.invalid_argument`, `command.error.too_many_arguments`,
      `command.error.parse_error` exist in `iv` and `ru-RU` (new `commands.loc`, namespace `command`).
- [ ] The solution builds.

## Answer

<!-- appended on resolution -->

## Comments

- Split of ownership agreed in the grilling session: the maintainer writes the RCParsing grammar, the agent ships the
  scaffold and the tests.
- **Agent half delivered.** `SlashCommands/Arguments/` carries every type, a fully implemented and unit-tested
  `SlashCommandArgumentBinder` (17 tests) and the `SlashCommandArgumentParser` scaffold (`Parser` / `Parse` / `TryParse`
  throw `NotImplementedException` behind a `TODO` pointing here). The ten grammar-contract tests are in place and
  `Skip`ped with `RCParsing grammar pending (maintainer)`.
- **Remaining:** the maintainer's RCParsing grammar in `SlashCommandArgumentParser`; the ticket resolves once the ten
  grammar tests are un-skipped and green. Locale domain `command` was registered in `Localization/Resources/RULES.md`.
- **`Raw` and `Ready` in the bound result.** `ParsedSlashCommandArgument.Raw` is the `SlashCommandRawArgument` the user
  wrote — text, quotedness and span — or `null` when the value came from a schema default or was not supplied; `Ready`
  is derived from it (`Raw?.Unescaped ?? Definition.Default`) and is the text `Value` is converted from. This is what
  carries the source span past the binder, and it drops the old `string Raw` whose meaning contradicted the raw
  argument's own `Raw`.
- **Spans added.** `SlashCommandRawArgument` records where in `RawArguments` it sits: `Position`/`Length` for the whole
  occurrence, `ValuePosition`/`KeyLength` for the value and the key, `ValueLength` computed as
  `Position + Length - ValuePosition` (always `Raw.Length`). A positional is its own value, so there
  `ValuePosition == Position` and `KeyLength == 0`. This is what makes an error locatable at all.
- **Contract amended.** The positional text split into two members: `RawPositionalArguments` now carries **every**
  positional argument verbatim (the whole region before the first key), and the rest positional moved to its own
  `RestPositionalArguments` (the surplus beyond the declared positionals — a slice of the former, identical to it when
  no positionals are declared). The rest positional is no longer "the whole remainder": the declared positionals are
  parsed as usual. `SlashCommandRawArgument` also records the pair the implementation exposes — `Raw` (quotes present)
  and `Unescaped` (quotes stripped, `\"` unescaped).
