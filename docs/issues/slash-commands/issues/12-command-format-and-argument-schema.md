# Command formats and argument schema

Status: resolved
Type: grilling
Blocked by: 01

## Question

Design the authorable **command formats** and the `SlashCommandArgumentSchema` that falls out of them.

- The authored form of a command per source: **native** (code-registered `ISlashCommandProvider`), **derived** (from skill / sub-agent addons), and the future **file / scriptable** forms.
- The `SlashCommandArgumentSchema` model: typed vs free-form arguments, completion sources, validation (v1 or later).
- How the format relates to the existing frontmatter machinery, `ParameterSchema`, and LLT `@params`.

Carved out of ticket 01, where the argument-schema question was explicitly deferred ("much more complex; depends on the command formats").

## Answer

**Scope.** Both the v1 code format and the future file/scriptable formats are designed here; only v1 is implemented. The file parser and the scriptable engine are **contracts**, not implementation.

### Argument grammar (v1)

- The command token is `/ns:cmd` (or bare `/cmd`) at the **start** of the message; the remainder is the argument text.
- `RawArguments` = the whole remainder (after the token, leading whitespace trimmed).
- A **new argument tokenizer** is written — `ShellCommandSplitter` is *not* reused (it splits compound shell commands by `; && ||`). It: separates on whitespace, groups `'…'` / `"…"` and **strips** the quotes, treats `\` as escaping `"` inside double quotes, and marks each token as "was quoted".
- A **single left-to-right resolver**:
  - `key=…` where `key` is declared in the schema's `Keyed` set → starts a **keyed argument**;
  - an **unquoted** keyed value runs to the next schema-declared key (or the end) — spaces allowed; a **quoted** value (`key="a b"`) bounds it explicitly;
  - a `key=value` with an **undeclared** key is **not** an argument — it stays plain text (`x = y + 1` must not be swallowed);
  - everything else is a **positional** token.
- **Mixed (positional + keyed) is allowed in v1**: positionals precede the first key; after a key, tokens are absorbed into that key's value. Quoting a keyed value exists for **symmetry** with positional arguments (positionals may be quoted, so keyed values may be too) — it is not boundary protection.
- If the schema declares a **rest positional** (the "big" argument) the remainder is **not** split and is delivered raw as `RawPositionalArguments` — the sub-agent case.

### `SlashCommandArgumentSchema`

```csharp
public class SlashCommandArgumentSchema
{
    public ImmutableList<SlashCommandArgument> Positionals { get; set; } = [];
    public ImmutableDictionary<string, SlashCommandArgument> Keyed { get; set; } = [];
    public bool HasRestPositional { get; set; }   // "big" argument: the remainder is delivered raw
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

- No `ValueType` / `Choices`.
- **Command / skill / agent names are NOT command arguments.** Completing names and namespaces is the job of a **general autocomplete service** (tickets 08/09). `ISlashCommandArgumentFormatProvider` handles only per-argument logic: files, "one of many", etc.

### `ISlashCommandArgumentFormatProvider`

```csharp
public interface ISlashCommandArgumentFormatProvider
{
    bool TryValidate(string raw, out LocaleKeyBase? error);   // error is a locale key
    object? Convert(string raw);                              // conversion to the needed format
    bool CanComplete { get; }
    IEnumerable<SlashCommandCompletionItem> Complete(         // synchronous, not async
        string prefix, SlashCommandCompletionContext ctx);
}
```

### Parsed arguments & execution context

```csharp
public class ParsedSlashCommandArgument
{
    public SlashCommandArgument Definition { get; init; }
    public string Raw { get; init; }
    public object? Value { get; init; }   // ISlashCommandArgumentFormatProvider.Convert
}
// SlashCommandExecutionContext adds:
//   Positionals : ImmutableList<ParsedSlashCommandArgument>
//   Keyed       : ImmutableDictionary<string, ParsedSlashCommandArgument>
//   RawPositionalArguments, RawArguments, GenerateIntent, Message, Chat, Services
```

### File & scriptable formats (designed, not implemented)

- Pack folder `commands/`: `commands/<name>.md` / `commands/<name>/COMMAND.md` — **"skill" commands** (their body is injected); plus script extensions.
- `SlashCommandParser : FrontmatterBasedAddonParser<SlashCommandInfo, SlashCommandChange>`, `[Service(typeof(IAddonFileParser<SlashCommandInfo>))]`; descriptor like `SkillParser` (`---`, `RequiresFrontmatter = false`, `UseMarkdownFallback = true`); its **own thin `Populate`** — `SkillInfo`'s body and frontmatter are **not** reused.
- Frontmatter keys: the base set (`name` / `description` / `title` / `category` / `order` / `aliases`) + `namespaces`, `model-facing`, `generate`, `argument-schema` (YAML mirroring `SlashCommandArgumentSchema`, parsed by a dedicated property parser).
- Scriptable: `IScriptableCommandEngine` mirroring `IScriptableToolEngine` (`Language`, `Extensions`, `FrontmatterStart` / `FrontmatterEnd`, `CreateExecutor(SlashCommandInfo)`); the engine is selected by file extension, as in `ScriptableToolParser`.
- File commands arrive via the **non-additional** set (`IAddonAccessor<SlashCommandInfo>`); native/derived commands via `GetAdditionalAddons()`.

### Confirmed typical shapes

1. Sub-agents — one big **raw** positional argument.
2. Skills — plain positional arguments.
3. Tools — plain keyed arguments.
4. Tools v2 — a big unkeyed argument **+** keyed arguments (future; needs a schema richer than JSON-schema that can mark a "main" argument).

**Validation timing (settled in ticket 05):** argument validation *does* run in v1 — at **send** time, through `ISlashCommandArgumentFormatProvider.TryValidate`; a command that does not exist, or one whose arguments fail validation, **blocks the send** (nothing is inserted). The `Completer` stays UX-only.

## Comments

- Round 1: scope = both formats; new `SlashCommandArgumentSchema`; shell-like tokenization with stripped quotes; "additional" = the collector's `GetAdditionalAddons()` (code sources registered as `ISlashCommandProvider`), **not** a frontmatter set; `.md` files are "skill" commands; further extensions supply `IScriptableCommandEngine`.
- Round 2 corrections: no `ValueType`/`Choices`; `ISlashCommandArgumentFormatProvider` owns validation + completion + conversion; error is a `LocaleKeyBase`; completion is `IEnumerable`; command/skill/agent names are completed by the general autocomplete service, not per-argument; quotes on keyed values are for symmetry.
- Draft reviewed by the user and accepted with those two corrections.
