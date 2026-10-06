# 07: Command argument grammar and schema

Status: open
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
    public ImmutableList<SlashCommandArgument> Positionals { get; set; } = [];
    public ImmutableDictionary<string, SlashCommandArgument> Keyed { get; set; } = [];
    public bool HasRestPositional { get; set; }   // the "big" argument: the remainder is delivered raw
}

public class SlashCommandArgument
{
    public required LocaleKeyBase Name { get; set; }
    public LocaleKeyBase? Description { get; set; }
    public bool Required { get; set; }
    public string? Default { get; set; }
    public ISlashCommandArgumentFormatProvider? Format { get; set; }
}
```

No `ValueType`, no `Choices`.

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
    public required string RawPositionalArguments { get; init; }     // the rest blob (only when HasRestPositional)
    public required ImmutableList<SlashCommandRawArgument> Positionals { get; init; }
    public required ImmutableDictionary<string, SlashCommandRawArgument> Keyed { get; init; }
    public LocaleKeyBase? Error { get; init; }                       // syntax error (unterminated quote, ...)
}

public sealed class SlashCommandRawArgument
{
    public SlashCommandArgument? Definition { get; init; }           // schema slot; null = surplus positional
    public required string Raw { get; init; }                        // quotes stripped, `\"` unescaped
    public required bool WasQuoted { get; init; }
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
- `HasRestPositional`: the remainder is **not** split into positional tokens; declared `key=…` segments are extracted
  and everything else is delivered **verbatim** (quotes kept) as `RawPositionalArguments`, which is what the `/agent`
  command forwards into `AgentUserMessage.Content`.

### Binder (fully implemented here)

```csharp
public sealed class ParsedSlashCommandArgument
{
    public required SlashCommandArgument Definition { get; init; }
    public required string Raw { get; init; }
    public object? Value { get; init; }   // Format is null ? Raw : Format.Convert(Raw)
}

public sealed class SlashCommandBoundArguments
{
    public required ImmutableList<ParsedSlashCommandArgument> Positionals { get; init; }
    public required ImmutableDictionary<string, ParsedSlashCommandArgument> Keyed { get; init; }
    public required string RawPositionalArguments { get; init; }
    public LocaleKeyBase? Error { get; init; }
}

public static class SlashCommandArgumentBinder
{
    public static SlashCommandBoundArguments Bind(
        SlashCommandArgumentSchema schema, SlashCommandArgumentsResult parsed);
}
```

`Bind` rules, in order:

1. a parser syntax error is propagated as the result error;
2. positionals by index — absent with `Required` and no `Default` → `command.error.missing_argument`;
   surplus positionals (beyond the schema) → `command.error.too_many_arguments`;
3. keyed — for each **declared** key: present → its raw value; absent with `Default` → the default; absent with
   `Required` and no `Default` → `command.error.missing_argument`;
4. `Format.TryValidate(raw, out error)` for every **user-supplied** value (`Default`s are authored and trusted) —
   a failure yields that error and **blocks the send** (ticket 05);
5. `Value = Format is null ? Raw : Format.Convert(Raw)` — conversion runs only after validation passed.

## Acceptance criteria

- [ ] `SlashCommandArgumentSchema`, `SlashCommandArgument`, `ParsedSlashCommandArgument`, `SlashCommandRawArgument`,
      `SlashCommandArgumentsResult`, `SlashCommandBoundArguments`, `ISlashCommandArgumentFormatProvider`,
      `SlashCommandCompletionItem`, `SlashCommandCompletionContext` exist under `SlashCommands/Arguments/`.
- [ ] `SlashCommandArgumentParser` exists with the public shape above; `Parse`/`TryParse` route through the RCParsing
      `Parser` and the schema metadata factory.
- [ ] `SlashCommandArgumentBinder.Bind` is implemented and unit-tested for all five rules above.
- [ ] The grammar contract is covered by tests (whitespace, quote stripping, `\"`, undeclared `key=`, unquoted value
      running to the next declared key, quoted value bounding, mixed positional-before-key, rest-positional verbatim
      with declared keys extracted). While the grammar is pending these are `Skip`ped with
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
