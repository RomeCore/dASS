# 10: Command set collector, settings and DI

Status: open
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

    protected override object GetDeduplicationKey(SlashCommandInfo addon)
        => string.Join(':', addon.Namespaces.OrderBy(n => n, StringComparer.Ordinal)) + ":" + addon.Name;

    public override IEnumerable<SlashCommandInfo> GetAddonsForChat() { ... }
}
```

- The dedup key is the **fully-qualified** name: namespaces sorted ordinally, then the command name last — e.g.
  `/skill:grilling` → `"skill:grilling"`, `/skill:matt-pocock:grilling` → `"matt-pocock:skill:grilling"`. Commands of
  different types/packs never collapse into one another; same-type/same-pack duplicates collapse and the losers land in
  `Overrides` (the base collector already does this).
- `GetAddonsForChat()` is the only exposed path (commands are chat-level and agent-agnostic):
  - `!chatSettings.Settings.Commands.GetEffectiveEnableCommands()` → `[]`;
  - otherwise `GetAddonsWithChanges(chatSettings.Settings.Commands.GetEffectiveCommandsSet(), agent: null)`.

### Settings

```csharp
[SettingsRoute(nameof(ChatSettings.Commands))]
public partial class ChatCommandSettings : ChatSettingsCategoryBase
{
    [InheritedChatSetting] public bool EnableCommands { get; set; } = true;
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

- [ ] `SlashCommandSetCollector` is registered as `IAddonSetCollector<SlashCommandInfo>` and pulls every
      `ISlashCommandProvider` through `GetAdditionalAddons()`.
- [ ] `GetDeduplicationKey` is the fully-qualified key; a unit test proves `skill:grilling` and
      `skill:matt-pocock:grilling` stay distinct and that a true duplicate collapses into `Overrides`.
- [ ] `ChatCommandSettings` exists with `EnableCommands` and `CommandsSet`, both `[InheritedChatSetting]`, and
      `ChatSettings.Commands` exposes it.
- [ ] A unit test proves the master gate: `EnableCommands = false` → `GetAddonsForChat()` is empty; `true` → the
      commands are returned.
- [ ] A unit test proves a `SlashCommandSet.Changes` entry for a command name disables/enables that command.
- [ ] The app and desktop solutions build; the full test suite stays green.

## Answer

<!-- appended on resolution -->

## Comments

- The design ticket adds "room for per-command overrides by name": the base `Changes` dictionary keyed by `Name` covers
  it; no richer keying is introduced in v1.
