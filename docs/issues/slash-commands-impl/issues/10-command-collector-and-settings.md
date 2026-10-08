# 10: Command set collector, settings and DI

Status: resolved
Type: task
Blocked by: 09

## What to build

The collector that merges the providers into the chat's command set, the fully-qualified deduplication key, and the
chat-level enablement settings behind it.

Files: `src/LLMDesktopAssistant/SlashCommands/SlashCommandSetCollector.cs`,
`src/LLMDesktopAssistant/SlashCommands/SlashCommandSet.cs`, `src/LLMDesktopAssistant/LLM/Settings/ChatCommandSettings.cs`.

### Collector

```csharp
[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]
public class SlashCommandSetCollector(
    IEnumerable<ISlashCommandProvider> providers,
    IChatSettingsService chatSettings,
    IServiceProvider services
) : AddonSetCollectorBase<SlashCommandInfo, SlashCommandChange>(services)
{
    protected override IEnumerable<SlashCommandInfo> GetAdditionalAddons()
        => providers.SelectMany(p => p.GetCommands());

    // Deduplication and change resolution use SlashCommandInfo.Key (the fully-qualified identity); no collector
    // override is needed — the base collector groups by AddonChangedBase.Key.

    public override IEnumerable<SlashCommandInfo> GetAddonsForChat() { ... }
}
```

- `SlashCommandInfo.Key` is the **fully-qualified** name (and the change key too, since `AddonSetCollectorBase` now deduplicates and resolves changes by `AddonChangedBase.Key`): namespaces sorted ordinally, then the command name last — e.g.
  `/skill:grilling` → `"skill:grilling"`, `/skill:matt-pocock:grilling` → `"matt-pocock:skill:grilling"`. Commands of
  different types/packs never collapse into one another; same-type/same-pack duplicates collapse and the losers land in
  `Overrides` (the base collector already does this).
- `GetAddonsForChat()` is the only exposed path (commands are chat-level and agent-agnostic):
  - `!chatSettings.Settings.Commands.EnableCommands` → `[]`;
  - otherwise `GetAddonsWithChanges(chatSettings.Settings.Commands.GetEffectiveCommandsSet(), agent: null)`.

### Settings

```csharp
[SettingsRoute(nameof(ChatSettings.Commands))]
public partial class ChatCommandSettings : ChatSettingsCategoryBase
{
    public bool EnableCommands { get; set; } = true;              // chat-local, NOT inherited
    [InheritedChatSetting] public SlashCommandSet CommandsSet { get; set; } = new();
}

public class SlashCommandSet : AddonSetConfigurationBase<SlashCommandChange> { }
```

and `ChatSettings` gains `public ChatCommandSettings Commands { get; set; }`.

`EnableCommands` is the master gate (chat-level, inheritable from the application); `CommandsSet` is the per-command
override bag (`Changes` keyed by command name, `EnabledByDefault = true`, `HiddenByDefault = false`). The generated
effective getters (`GetEffectiveEnableCommands()` / `GetEffectiveCommandsSet()`) are used — no hard-coded effective
values.

### DI

All wiring is attribute-based and already generic: `[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]` on the
collector; the descriptor/locator/parser from ticket 08 are app-scoped `[Service]`/`[AddonTypeDescriptor]` singletons and
are picked up by `AddonServicesConfigurator`, which also registers the chat-scoped accessor/loader. No manual
registration, no `ServiceConfigurator` change.

## Acceptance criteria

- [x] `SlashCommandSetCollector` is registered as `IAddonSetCollector<SlashCommandInfo>` and pulls every
      `ISlashCommandProvider` through `GetAdditionalAddons()`.
- [x] `SlashCommandInfo.Key` is the fully-qualified key; a unit test proves `skill:grilling` and
      `skill:matt-pocock:grilling` stay distinct and that a true duplicate collapses into `Overrides`.
- [x] `ChatCommandSettings` exists with `EnableCommands` (chat-local) and `CommandsSet` (`[InheritedChatSetting]`),
      and `ChatSettings.Commands` exposes it.
- [x] A unit test proves the master gate: `EnableCommands = false` → `GetAddonsForChat()` is empty; `true` → the
      commands are returned.
- [x] A unit test proves a `SlashCommandSet.Changes` entry for a command name disables/enables that command.
- [x] The app and desktop solutions build; the full test suite stays green.

## Answer

Implemented: `SlashCommands/SlashCommandSet.cs`, `SlashCommands/SlashCommandSetCollector.cs`,
`LLM/Settings/ChatCommandSettings.cs`, plus `ChatSettings.Commands`.

### Deduplication and change resolution: `AddonChangedBase.Key`

During the grilling session the per-collector `GetDeduplicationKey` (added in an earlier ticket) was **replaced by a
single `virtual string AddonChangedBase.Key => Name`** with the triple `[JsonIgnore][BsonIgnore][YamlIgnore]`.
`AddonSetCollectorBase` now groups by `addon.Key` and resolves `Changes` by `addon.Key`; `AddonCardContext` does the
same. This closes a real bug: `Changes` keyed by bare `Name` could not tell `skill:grilling` from `agent:grilling`
(two distinct commands that share a name), so overriding one leaked onto the other.
`SlashCommandInfo` overrides `Key` to the **fully-qualified** identity — namespaces sorted ordinally, then the name,
joined by `:` (e.g. `matt-pocock:skill:grilling`). The collector therefore needs no key override at all.
(`ToolConsentPersister` still writes by tool name — harmless, tools have `Key == Name`.)

### Settings

`ChatCommandSettings` (route `ChatSettings.Commands`) carries `CommandsSet : SlashCommandSet`,
**`[InheritedChatSetting]`**, and `EnableCommands : bool = true`, deliberately **chat-local and not inherited**
(the master gate is never taken from the application). `SlashCommandSet : AddonSetConfigurationBase<SlashCommandChange>`
lives in `SlashCommands/` (like `SkillsetSettings` lives in `Agents/Settings/`).

### Collector

`SlashCommandSetCollector` (`[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]`) pulls every
`ISlashCommandProvider` through `GetAdditionalAddons()`. `GetAddonsForChat()` is the only exposed path:
`!Commands.EnableCommands` → `[]`, otherwise `GetAddonsWithChanges(Commands.GetEffectiveCommandsSet(), agent: null)`.
`GetAddonsForAgent()` is deliberately left unimplemented (commands are not agent-bound), so any attempt to resolve them
per agent is noisy. DI stays attribute-based — nothing manual.

### Tests

`SlashCommandSetCollectorTests` (6): distinct namespaces stay distinct, true duplicates collapse into `Overrides`,
the master gate both ways, a change disables its command, and a change does **not** affect a same-named command of
another namespace. `AddonSetCollectorDeduplicationTests` was rewritten to override `Key` instead of
`GetDeduplicationKey`.

Main + desktop builds green; full suite: 858 total, 857 passed, 1 skipped (pre-existing).

## Comments

- Per-command overrides are keyed by `SlashCommandInfo.Key` (the fully-qualified identity), not by bare name: the
  earlier `GetDeduplicationKey` was folded into `AddonChangedBase.Key`, now the single key for dedup **and** change
  resolution (collector + addon cards).
- `EnableCommands` is chat-local (not inherited); `CommandsSet` is inherited, as the ticket said.
