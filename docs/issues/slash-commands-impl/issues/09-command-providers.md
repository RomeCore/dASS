# 09: Command providers (skills, sub-agents)

Status: open
Type: task
Blocked by: 08

## What to build

The v1 command sources: a chat-scoped provider contract and the two derived providers that synthesise a
`SlashCommandInfo` for every skill and every sub-agent available to the chat.

Files live under `src/LLMDesktopAssistant/SlashCommands/Providers/`.

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

Both providers read the **whole** available set of their kind — `IAddonSetCollector<SkillInfo>.GetAvailableAddons()` /
`IAddonSetCollector<SubAgentInfo>.GetAvailableAddons()`, **not** the per-chat / per-agent filtered sets — and produce
exactly one command per addon:

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

- [ ] `ISlashCommandProvider` exists and is registered chat-scoped.
- [ ] `SkillSlashCommandProvider` and `SubAgentSlashCommandProvider` exist, each `[ChatService(typeof(ISlashCommandProvider))]`,
      and build one command per addon from `GetAvailableAddons()`.
- [ ] `SlashCommandOrderTiers` exists with the tiers above.
- [ ] Mapping is unit-tested: name/description/aliases/order copied; namespaces `[type]` without a pack and
      `[type, pack]` with one; `Enabled`/`Hidden` are `null`; `OverrideOrder == Derived`; `ModelFacingMode == Raw`;
      `Generate == null`; executor is the stub.
- [ ] The per-type argument schemas are unit-tested (skill = rest positional; agent = rest positional + optional `wait`
      with default `"false"`).
- [ ] A command produced by a provider is frozen (mutating it throws).
- [ ] Locale keys `command.argument.wait` / `command.argument.wait.description` exist in `iv` and `ru-RU`.
- [ ] The solution builds; the full test suite stays green.

## Answer

<!-- appended on resolution -->

## Comments

- Derived commands are produced from *all* addons of the kind, deliberately ignoring `GetAddonsForChat()` /
  `GetAddonsForAgent()` filtering (design ticket 01): the command surface is chat-level and agent-agnostic, and a
  command's own on/off state is owned by the command set (ticket 10), not by the source addon.
