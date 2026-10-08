# 09: Command providers (skills, sub-agents)

Status: resolved
Type: task
Blocked by: 08

## What to build

The v1 command sources: a chat-scoped provider contract and the two derived providers that synthesise a
`SlashCommandInfo` for every skill and every sub-agent available to the chat.

Files live under `src/LLMDesktopAssistant/SlashCommands/Providers/` (`SlashCommandOrderTiers` itself lives at the `SlashCommands/` root, because the resolver also reads `OverrideOrder`).

### Provider contract

```csharp
public interface ISlashCommandProvider
{
    IEnumerable<SlashCommandInfo> GetCommands();
}
```

Providers are chat-scoped services registered as `[ChatService(typeof(ISlashCommandProvider))]`; the collector
(ticket 10) pulls them through `GetAdditionalAddons()`. Commands are built and `Freeze()`d inside the provider
(the `PromptContextNativeProvider` pattern).

### Derived mapping

A generic `DerivedSlashCommandProvider<TSource>` reads its source collector's `GetAvailableAddons()` and keeps only
**valid** addons (`AddonBase.IsValid`, diagnostics) — **not** the per-chat / per-agent filtered sets — and produces
exactly one command per valid addon. `SkillSlashCommandProvider` / `SubAgentSlashCommandProvider` are the two v1
concretes:

| `SlashCommandInfo` field | value |
|---|---|
| `Name` | source `Name` |
| `Description` | source `Description` |
| `Aliases` | source `Aliases` |
| `Order` | source `Order` |
| `Namespaces` | `[type, (SourcePack?.Name when present)]` — type **first**, pack second |
| `OverrideOrder` | `SlashCommandOrderTiers.Derived` |
| `Enabled` / `Hidden` | `null` — a derived command does **not** inherit the source's effective enablement |
| `ModelFacingMode` | `ModelFacingMode.Raw` |
| `Generate` | `null` |
| `Executor` | `StubCommandExecutor.Instance` (real executors are Stage 3) |
| `ArgumentSchema` | see below |
| `SourcePack` / `Path` / `AddonSource` | copied from the source (provenance) |
| `Source` | the source addon instance, typed `object?` (the addon bases are CRTP-parameterized) |

Types: `skill` for `/skill:<name>`, `agent` for `/agent:<name>` (`tool` and `script` are reserved, not produced here).

Source-priority tiers (design ticket 02: "providers assign `OverrideOrder`"):

```csharp
public static class SlashCommandOrderTiers
{
    public const int Derived = 0;
    public const int Scriptable = 1000;
    public const int Native = 2000;
}
```

### v1 argument schemas

- **skill** (`/skill:<name> args…`) — the whole argument text is substituted into the body
  (`$ARGUMENTS` / named placeholders), so the schema is a single rest positional:
  `new SlashCommandArgumentSchema { HasRestPositional = true }`.
- **agent** (`/agent:<name> input`) — the input is the rest positional and `wait` is an optional boolean key.
  The schema is `init`-only (ticket 07), so the keyed argument is built through a local builder (a collection
  initializer on an `ImmutableDictionary` is invalid C# — it would call the `Add` overload that returns a new map):

  ```csharp
  var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
  keyed["wait"] = new SlashCommandArgument
  {
      Name = Locale.GetKey("command.argument.wait"),
      Description = Locale.GetKey("command.argument.wait.description"),
      Required = false,
      Default = "false"
  };

  var schema = new SlashCommandArgumentSchema { HasRestPositional = true, Keyed = keyed.ToImmutable() };
  ```

The concrete `wait=true|false` `ISlashCommandArgumentFormatProvider` is **out of scope** here: its validation lands with
the `/agent` executor (Stage 3) and its completion is wired in Stage 5. Stage 1 only declares the schema.

## Acceptance criteria

- [x] `ISlashCommandProvider` exists and is registered chat-scoped.
- [x] `SkillSlashCommandProvider` and `SubAgentSlashCommandProvider` exist, each `[ChatService(typeof(ISlashCommandProvider))]`,
      and build one command per addon from `GetAvailableAddons()`.
- [x] `SlashCommandOrderTiers` exists with the tiers above.
- [x] Mapping is unit-tested: name/description/aliases/order copied; namespaces `[type]` without a pack and
      `[type, pack]` with one; `Enabled`/`Hidden` are `null`; `OverrideOrder == Derived`; `ModelFacingMode == Raw`;
      `Generate == null`; executor is the stub.
- [x] The per-type argument schemas are unit-tested (skill = rest positional; agent = rest positional + optional `wait`
      with default `"false"`).
- [x] A command produced by a provider is frozen (mutating it throws).
- [x] Locale keys `command.argument.wait` / `command.argument.wait.description` exist in `iv` and `ru-RU`.
- [x] The solution builds; the full test suite stays green.

## Answer

Implemented under `src/LLMDesktopAssistant/SlashCommands/Providers/` plus a root `SlashCommandOrderTiers.cs`.

### Provider contract

A single generic base, `DerivedSlashCommandProvider<TSource>` (`where TSource : AddonBase<TSource>`), with three
abstract points and one hook: `TypeNamespace` (the type qualifier, `skill` / `agent`),
`CreateArgumentSchema(TSource source)` (source-dependent on purpose — a source may shape its schema later),
`CreateCommandExecutor(TSource source)` (an explicit contract so the executor cannot be forgotten; the base assigns
it), and a no-op `PopulateFromSource(TSource source, SlashCommandInfo command)` hook for further per-kind copying.
`SkillSlashCommandProvider` / `SubAgentSlashCommandProvider` are the two `[ChatService(typeof(ISlashCommandProvider))]`
concretes.

### Source set and validity (reworked from the ticket's original wording)

The provider reads its source collector's `GetAvailableAddons()` and keeps only valid addons — it does **not** call
`GetAddonsForChat()`, whose availability predicates are a prompt concern. Availability filtering now has one home:
`AddonBase.IsValid` (a virtual, computed, `[JsonIgnore][BsonIgnore][YamlIgnore]` property,
`=> Diagnostic?.IsFatal is not true`), which `AddonSetCollectorBase` uses in place of the three inlined
`Diagnostic?.IsFatal` checks. The command surface's own `GetAddonsForChat()` (the command collector, ticket 10) is what
autocomplete and resolution call.

### Mapping

`Name` / `Description` / `Aliases` / `Order` are copied; `Namespaces` is `[TypeNamespace, (SourcePack?.Name when present)]`;
`OverrideOrder = SlashCommandOrderTiers.Derived`; `Enabled` / `Hidden` are `null`; `ModelFacingMode = Raw`;
`Generate = null`; `Executor` comes from `CreateCommandExecutor`. Provenance is carried over too — `SourcePack`,
`Path`, `AddonSource` and the new `object? Source` (the source addon instance) — so the fingerprint's `Source` and the
UI can trace a command back to its origin. Commands are `Freeze()`d before they leave the provider.

### Schemas

- skill — `new() { HasRestPositional = true }`.
- agent — rest positional plus an optional `wait` key (`Default = "false"`, locale keys `command.argument.wait` /
  `command.argument.wait.description`).

### Tests

`SlashCommandProviderTests` (7): mapping incl. namespaces with/without a pack and provenance, one command per available
addon, invalid-addon filtering, frozen commands, and both schemas.

Locale keys added to `iv` and `ru-RU` `commands.loc`. Main + desktop builds green; full suite: 852 total, 851 passed,
1 skipped (pre-existing).

## Comments

- The ticket originally read from *all* addons of the kind, ignoring filtering. The grilling session inverted the
  source to `GetAvailableAddons().Where(a => a.IsValid)`: a broken source should not surface a dead command, and
  availability filtering now lives in one place (`IsValid`).
- `GetAddonsForChat()` stays the consumers' entry point — but on the *command* collector (ticket 10), not on the source
  collectors; that level split is why the provider filters manually instead of calling it (which would also emit the
  base collector's "not implemented" warning for the skill/sub-agent collectors).
- A command's own on/off state is owned by the command set (ticket 10), not by the source addon.
