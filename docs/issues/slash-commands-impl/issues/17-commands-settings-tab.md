# 17: Chat commands settings tab

Status: resolved
Type: task
Blocked by: 16

## What to build

A chat-level settings page for slash commands, under the ADDONS section: a master enable gate, the command set
selection (with its inheritance level) and the available commands rendered by the reusable addon list, where every
card edits the command overrides of the effective command set. The ADDONS section is reordered and the scripts tab is
renamed while here.

### Card factory

`SlashCommandAddonCardFactory : AddonCardFactoryBase<SlashCommandInfo, SlashCommandChange>`
(`[Service(typeof(IAddonCardFactory<SlashCommandInfo, SlashCommandChange>))]`) in `SlashCommands/`:

- `TypeIcon = Console`;
- replaces the default enabled/hidden editor with the **enabled-only** one (`AddonCardEnabledHiddenChange` →
  `AddonCardEnabledChange`) and the group editor with `AddonCardGroupEnabledChange` — a command has no hidden mode (it
  is never injected into a prompt and never exposed to agents);
- adds a source-kind chip built from `SlashCommandInfo.SourceKind` (ticket 16).

### Page

- `ChatCommandsSettingsViewModel` + `ChatCommandsSettingsView.axaml`: the master `EnableCommands` toggle, the
  `CommandsSetInheritance` selector (`InheritanceLevelItem.AllProfile`) and the addon list. The list is built with a
  context builder whose `SetConfig` is the effective command set (`ChatCommandSettings.GetEffectiveCommandsSet()`), so
  the cards edit the enabled overrides.
- Registered as a leaf node in `ChatSettingsViewModel.InitializeTree`.

### Ordering and rename

- The ADDONS children are reordered to: **tools, skills, sub-agents, memory, commands, prompt contexts, Lua scripts**.
- The scripts tab is renamed from "Scripts" to **"Lua scripts"** / "Lua-скрипты" (`settings.chat.scripts`).

### Search

- No `IAddonSearchService<SlashCommandInfo>` is registered (`UseDefaultSearchService = false`) and commands are
  user-only, so the list is built with a `null` search service and falls back to the plain substring match. The BM25
  search stays off so commands never leak into the agentic addon search.

## Acceptance criteria

- [x] `SlashCommandAddonCardFactory` exists, is registered for `IAddonCardFactory<SlashCommandInfo, SlashCommandChange>`
      and uses enable-only toggles plus a `SourceKind` chip.
- [x] `ChatCommandsSettingsViewModel` + `ChatCommandsSettingsView` exist; the page carries the `EnableCommands` gate,
      the `CommandsSetInheritance` selector and an editable command list.
- [x] The commands leaf node is added to the chat settings tree under ADDONS.
- [x] The ADDONS children are ordered tools → skills → sub-agents → memory → commands → prompt contexts → Lua scripts.
- [x] The scripts tab is renamed to "Lua scripts" (iv) / "Lua-скрипты" (ru).
- [x] Localization keys added (`settings.chat.commands`, `settings.commands.*`, `card.commands.source.*`) in both `iv`
      and `ru-RU`.
- [x] The solution builds (main + desktop); the (filtered) test suite stays green.

## Answer

- **Card factory** (`SlashCommands/SlashCommandAddonCardFactory.cs`): enable-only header/group toggles, `Console` icon,
  and a `card.commands.source.<kind>` chip whose icon/label follow `SourceKind`.
- **Page** (`LLM/MVVM/Settings/ChatCommandsSettingsViewModel.cs` + `ChatCommandsSettingsView.axaml(.cs)`): the master
  gate is bound to `CommandSettings.EnableCommands` (chat-local, not inherited), the set selector drives
  `CommandsSetInheritance`, and the list is editable — the context builder sets `SetConfig = EffectiveCommandsSet`, so a
  card writes a `SlashCommandChange` (enabled override) into the effective set. The list is built with `null` search
  (substring fallback). The page is deliberately **editable at chat level** (unlike the read-only chat skills/sub-agents
  pages) because commands have no per-agent page.
- **Registration.** A `Commands` leaf (`MaterialIconKind.Console`) is added to the ADDONS parent in
  `ChatSettingsViewModel.InitializeTree`, resolving the collector/factory/invalidator from `Chat.Services`.
- **Ordering.** The ADDONS children are reordered to tools, skills, sub-agents, memory, commands, prompt contexts,
  scripts. The scripts tab keeps the `settings.chat.scripts` key with the value changed to "Lua scripts" / "Lua-скрипты".
- **Localization.** `settings.loc` (iv + ru) gains `chat.commands` and the `commands.*` block and the script-tab rename;
  `card.loc` (iv + ru) gains the `commands.source.*` section.

Builds: main + desktop green; slash-command set 127 passed; localization set 39 passed.

## Comments

- Out of the original plan: the plan carried no settings UI for commands before this ticket.
