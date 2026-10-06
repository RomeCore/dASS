# 08: Command model and executor contracts

Status: open
Type: task
Blocked by: 07

## What to build

The command addon model and the executor contract it declares, plus the addon-pipeline plumbing that lets
`SlashCommandInfo` participate in `AddonSetCollectorBase` even though v1 has no file-based commands.

Files live under `src/LLMDesktopAssistant/SlashCommands/` (`Execution/` and `Loading/` subfolders).

### Model

```csharp
public class SlashCommandInfo : AddonChangedBase<SlashCommandInfo, SlashCommandChange>
{
    public ImmutableList<string> Namespaces { get; set; } = [];        // ordered: type first, then pack
    public ModelFacingMode ModelFacingMode { get; set; } = ModelFacingMode.Raw;
    public bool? Generate { get; set; } = null;                        // null = leave the intent untouched
    public SlashCommandArgumentSchema? ArgumentSchema { get; set; }
    public ISlashCommandExecutor Executor { get; set; } = StubCommandExecutor.Instance;

    protected override void ValidatePropertiesCore(AppendOnlyList<string> errors)
    {
        base.ValidatePropertiesCore(errors);                           // Name + Description from the base
        if (Executor is null) errors.Add("Executor is required.");
    }
}

public class SlashCommandChange : AddonChangeBase { }                  // empty in v1 (mirrors PromptContextChange)

public enum ModelFacingMode { Raw, Neutral, Hidden }                   // v1 uses Raw only
```

`Executor` is **not** nullable (mirrors `PromptContextInfo.Provider`), but there are no real executors until Stage 3, so
its default is the temporary `StubCommandExecutor` scheduled for deletion.

`ArgumentSchema` keeps `{ get; set; }` even though the schema itself is `init`-only (ticket 07): the addon model is
mutable by design — `AddonBase.Clone()` copies every settable property of `SlashCommandInfo` into an unfrozen copy — so
the addon may hold a *different* schema, while the schema instance it points at never changes.

### Executor contract

```csharp
public interface ISlashCommandExecutor
{
    Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct);
}

public sealed class SlashCommandExecutionContext
{
    public required Chat Chat { get; init; }
    public required ChatMessage Message { get; init; }                 // the target, already inserted
    public required SlashCommandInfo Command { get; init; }
    public required string Token { get; init; }                        // e.g. "/skill:grilling"
    public required string RawText { get; init; }                      // the whole message text
    public required string RawArguments { get; init; }
    public required string RawPositionalArguments { get; init; }
    public required ImmutableList<ParsedSlashCommandArgument> Positionals { get; init; }
    public required ImmutableDictionary<string, ParsedSlashCommandArgument> Keyed { get; init; }
    public required bool GenerateIntent { get; init; }
    public required IServiceProvider Services { get; init; }           // chat scope
}

public readonly record struct SlashCommandExecutionResult(bool Generate, string? Error)
{
    public static SlashCommandExecutionResult Ok(bool generate = true) => new(generate, null);
}

// Temporary: deleted in Stage 3 when the /skill and /agent executors land.
public sealed class StubCommandExecutor : ISlashCommandExecutor
{
    public static readonly StubCommandExecutor Instance = new();
    public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
        => Task.FromResult(SlashCommandExecutionResult.Ok());
}
```

The executor **mutates the target message directly** through `ctx`; the host (Stage 2) owns insertion, the execution
token and the hand-off to generation.

Intent ceiling (ticket 05): `final = GenerateIntent && result.Generate && (Command.Generate ?? true)` — a command may
lower the intent, never raise it. `SlashCommandInfo.Generate` is the declarative ceiling.

### Addon-pipeline plumbing (deviation, sanctioned by the maintainer)

Ticket 01 says "no file locator/parser in v1", but `AddonSetCollectorBase` resolves
`IAddonAccessor<TAddon>` in its constructor, and `AddonAccessor<T>` needs a registered `IAddonTypeDescriptor`
(plus the auto-registered `AddonFileCachedLoader<T>`, which needs an `IAddonFileParser<T>`), while `ChatAddonManager`
resolves `IAddonFileLocator<T>`. Without all four the collector cannot be constructed. So add inert stubs, mirroring the
`PromptContext*` family:

```csharp
[AddonTypeDescriptor]
public class SlashCommandAddonTypeDescriptor : IAddonTypeDescriptor
{
    public string Type => "commands";
    public AddonKind Kind => AddonKind.SlashCommand;
    public Type ClrType => typeof(SlashCommandInfo);
    public bool UseDefaultDiagnosticFactory => true;
    public bool UseDefaultSearchService => false;                      // commands are not BM25-searched in v1
    public LocaleKeyBase NameKey => Locale.GetKey("addon.type.commands.name");
    public LocaleKeyBase? DescriptionKey => Locale.GetKey("addon.type.commands.description");
}

[Service(typeof(IAddonFileLocator<SlashCommandInfo>))]
public class SlashCommandFileLocator : AddonFileLocatorBase<SlashCommandInfo>
{
    public override string[] Folders => [];
    public override string[] Extensions => [];
    public override bool AllowShortFormat => true;
    public override string? FullFormatName => null;
}

[Service(typeof(IAddonFileParser<SlashCommandInfo>))]
public class SlashCommandParser : FrontmatterBasedAddonParser<SlashCommandInfo>
{
    protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
        => new() { FrontmatterStart = "---", FrontmatterEnd = "---" };
    protected override void Populate(SlashCommandInfo addon, AddonFrontmatterDocument frontmatter,
        ref AddonDiagnostic? diagnostic) { }
}
```

`FrontmatterBasedAddonParser<T>` takes **one** generic parameter (the design ticket's two-parameter spelling is a typo).

### Completion context extension

Add `SlashCommandInfo Command { get; init; }` to `SlashCommandCompletionContext` (deferred from ticket 07 because the
type did not exist yet).

## Acceptance criteria

- [ ] `SlashCommandInfo` carries `Namespaces`, `ModelFacingMode`, `Generate`, `ArgumentSchema` and a non-nullable
      `Executor` defaulting to `StubCommandExecutor.Instance`.
- [ ] `SlashCommandInfo` is constructible with `new()`, survives `Clone()` + `Freeze()` (the addon machinery) and
      `ValidatePropertiesCore` reports a missing executor.
- [ ] `ModelFacingMode`, `SlashCommandChange`, `ISlashCommandExecutor`, `SlashCommandExecutionContext`,
      `SlashCommandExecutionResult` and `StubCommandExecutor` exist with the shapes above.
- [ ] `SlashCommandAddonTypeDescriptor`, `SlashCommandFileLocator` and `SlashCommandParser` are registered and resolve;
      `UseDefaultSearchService` is `false`.
- [ ] `SlashCommandCompletionContext.Command` exists.
- [ ] Locale keys `addon.type.commands.name` / `addon.type.commands.description` exist in `iv` and `ru-RU` (`addon.loc`).
- [ ] The solution builds; the full test suite stays green (loading an empty `commands/` folder yields no addons).

## Answer

<!-- appended on resolution -->

## Comments

- The stub locator/parser/executor are a deliberate, maintainer-sanctioned deviation from ticket 01 ("no file
  locator/parser in v1"): they exist only so the addon pipeline can construct the collector. The executor stub is
  deleted in Stage 3; the locator/parser become the real `commands/` implementation when file commands land.
