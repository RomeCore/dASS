---
name: prompt-section-authoring
description: Add or change an SCM prompt section in dASS - the state/delta quartet, the provider registration, the section template file, the delta semantics and the anchor pitfalls that silently cost tokens. Use when adding a new section to the system prompt, changing what a section tracks, making a section switchable per agent, or debugging deltas that re-announce the whole prompt after a restart.
---

# Prompt section authoring (dASS)

A **section** is one tracked unit of prompt state (identity, skills, sub-agents, memory blocks, tools, working directories, the system reminder). It does four jobs: capture its state, render that state into system prompt bytes, notice that the state changed, and render the change as text. The anchor, the frozen header, the delta plumbing and the rebaselining are the framework's jobs, not the section's.

Read two things before writing a line: `src/LLMDesktopAssistant/Prompting/Context/Providers/WorkingDirectories/` (the smallest complete example, including a hand-written delta engine) and `src/LLMDesktopAssistant/Prompting/Resources/sections/working_directories.llt`. Copy their shape.

## 1. The quartet

Everything lives in `src/LLMDesktopAssistant/Prompting/Context/Providers/<Name>/`, one file per class:

| File | Type / interface | Notes |
|---|---|---|
| `<X>Item.cs` | plain class | A serializable snapshot item. **Public** auto-properties only, optional `Clone()`. No behaviour, no getters with logic. |
| `<X>SectionState.cs` | `: PromptSectionStateBase` | What the section knows: the list/values it will diff against later. Public properties. |
| `<X>SectionDelta.cs` | `: PromptSectionDeltaBase` | What changed, one field per concern. Public properties. |
| `<X>StateProvider.cs` | `IPromptSectionStateProvider<XSectionState>`, `[ChatService(typeof(...))]` | `XSectionState CaptureState(ChatAgentDescriptor agent)`. Reads settings/services, returns null only if the section must not exist at all. |
| `<X>StateRenderer.cs` | `IPromptSectionStateRenderer<XSectionState>`, `[ChatService(typeof(...))]` | `SystemPromptSnapshot Render(XSectionState state)` - `return templates.GetTextTemplate("<id>").Render(new { ... })`. |
| `<X>DeltaProvider.cs` | `IPromptSectionDeltaProvider<XSectionState, XSectionDelta>`, `[ChatService(typeof(...))]` | `XSectionDelta? CalculateDelta(XSectionState? anchorState, IEnumerable<XSectionDelta> existingDeltas, EffectiveChatContext context)`. |
| `<X>DeltaRenderer.cs` | `IPromptSectionDeltaRenderer<XSectionDelta>`, `[ChatService(typeof(...))]` | `string Render(XSectionDelta delta)`. |
| `<X>Section.cs` | `: PromptAnchoredSectionBase<XSectionState, XSectionDelta>` | Ctor `(IServiceProvider services)`; `override string Discriminator => "<kebab-name>"`. The base resolves the four services from the scoped provider. |
| `<X>SectionProvider.cs` | `: PromptContextNativeProvider`, `[ChatService(typeof(PromptContextNativeProvider))]` | `AddContext(new PromptContextInfo { ... })` (see 2). |

The **discriminator** is the section's identity: it is stamped onto the captured state, it keys the states inside the anchor, and it keys the issued deltas. Keep it stable forever - changing it means every existing chat matches nothing and re-announces the section in full.

## 2. Registration

```csharp
AddContext(new PromptContextInfo
{
    Name = "working-directories",              // addon name, kebab-case, equals the discriminator by convention
    Order = 50,                                // position in the header (table below)
    Variability = PromptSectionVariability.Rare,
    Description = "English, one line.",         // hardcoded English: used by docs and by addon search
    NameKey = Locale.GetKey("prompt.context.name.working-directories"),
    DescriptionKey = Locale.GetKey("prompt.context.description.working-directories"),
    IsFixed = false,                            // false = switchable per agent; true = always on
    Provider = new WorkingDirectoriesSection(services)
});
```

Order registry (a new section slots between its neighbours): `system-slot` 0, `identity` 10, `skills` 20, `sub-agents` 30, `memory-blocks` 40, `working-directories` 50, `tools` 100, `system-reminder` `int.MaxValue`.

- `IsFixed = true` means always enabled and never hidden: `AddonSetCollectorBase.GetAddonsWithChanges` forces `Enabled = true` and `Hidden = false`, so nobody can switch the section off. Only `IsFixed = false` gives a working toggle.
- `Variability` (`ConfigDriven`, `Rare`, `Frequent`, `PerRequest`) is display metadata for the settings cards; it drives no behaviour.
- `AddContext` sets `OverrideOrder = 1` and freezes the info.

## 3. The template file

`src/LLMDesktopAssistant/Prompting/Resources/sections/<section>.llt`, one file per section, holding the anchor template plus its delta template. The `<id>` in `@template <id>` is the global key the renderers ask for - the file name does not matter, and the csproj glob `Prompting\Resources\**\*.llt` picks up new subfolders automatically. Every existing template id is `<discriminator>_system_section` / `<discriminator>_system_section_delta`.

```llt
@template working_directories_system_section
{
	@metadata
	{
		lang: 'iv'
	}
	@if lines
	{
		@foreach line in lines
		{
		- @line
		}
	}
}
```

LLT rules that bite:

- The anonymous object passed to `Render(new { ... })` **is** the template's variable set: property names become the variables.
- `@if <expr>` skips on null, empty string and empty collection; a `string[]?` that is null skips too. Pass `null` instead of an empty array when there is nothing to show.
- `@foreach x in xs { ... }` iterates; `@x.y` reads a member; indentation inside the template body is part of the rendered text.
- Section text is added to the header in section order, joined with `\n`, and empty renderings are skipped - a section that renders whitespace disappears, it does not leave a blank line.

## 4. Delta semantics

`CalculateDelta` is called on every request while a live anchor exists, so it must be cheap and honest.

- `anchorState` is the state captured when the anchor was created - **and it is null when the anchor has no state for this section**: the section was switched off (or did not exist) at the anchor time. Null means "the agent knows nothing about this section" and the delta must announce the whole thing, not return null.
- `existingDeltas` are the deltas already issued against this anchor. The known state is **anchor state + all issued deltas applied in order**, never the anchor alone. Reconstruct it explicitly (see `WorkingDirectoriesDeltaProvider.Apply`) and return null when nothing changed.
- Announce a new item in full, a changed one field-by-field, hiding an item silently, and reordering silently. Report each concern in its own delta field so the rendered reminder stays self-describing.
- Pick the item identity deliberately: identity = (name, path) reports a rename as removal + addition, which is honest and simple.
- Removing the active item of a list is not a removal of the *section*: handle both concerns in the same delta.
- A section that is switched off disappears from the section list entirely, so no delta can be emitted for the removal: the frozen header still carries its old bytes and the agent is never told. That is a known SCM limitation - do not try to work around it from inside a delta provider.

## 5. Anchor pitfalls (each one already cost a session)

1. **Anchors are append-only and the header is frozen.** In `Hybrid` mode the header bytes come from the anchor, never from a fresh render. A section that changes silently is a section the agent does not know about, which is why the delta path above exists.
2. **Everything that reaches `AdditionalChatData` must be public.** Section states, deltas and supersede stamps are persisted through LiteDB, and LiteDB maps public members only - an `internal` property is written as nothing (`[BsonField]` does not save an internal member either). Symptom when it happens: after **every restart or chat reload** the section treats its anchor state as missing and re-announces its entire content as a delta (+17k tokens in one real case) while the prompt cache survives, because the prefix stayed byte-stable. Check `anchor.Sections.FirstOrDefault(s => s.Discriminator == section.Discriminator)` the moment a section behaves as if it has no history.
3. **`MaxVisibleRounds` is ignored in `Hybrid`** on purpose (a round window would evict the anchor). Do not reach for it when the prompt grows.

## 6. Localization

- `src/LLMDesktopAssistant/Localization/Resources/iv/prompt.context.loc` and its `ru-RU` twin, namespace `prompt.context`: `name.<section>`, `description.<section>`, plus `variability.*` labels.
- The addon type descriptor `PromptContextAddonTypeDescriptor` asks for `addon.type.context.name` / `.description` in `addon.loc` - easy to forget, and the addons settings page shows the raw key without them.
- `Description` stays hardcoded English (docs, addon search); the localized text lives in `DescriptionKey`. `AddonCardFactoryBase` renders `NameKey` and hides the subtitle when it equals `Name`, so a missing key shows the raw kebab name in the UI.

## 7. Making the section switchable

The toggle is per agent and lives in `AgentContextSettings.ContextSet` (`ContextSetSettings : AddonSetConfigurationBase<PromptContextChange>`, `[InheritedChatAgentSetting]`), resolved with `GetEffectiveContextSet(chatSettings)` - and there is no chat-level settings category, by design.

- Chat level: a read-only catalogue - `ChatPromptContextsSettingsViewModel` + `ChatPromptContextsView`, a leaf in `ChatSettingsViewModel` inside the addons group (`SettingsParentNode(addon.settings.title)`), built on `AddonListViewModel<PromptContextInfo, PromptContextChange>` with `SetConfig = null`.
- Agent level: the list at the end of the `Context` leaf - `AgentContextSettingsViewModel.List` with the context builder that passes `SetConfig = EffectiveContextSet`, plus the enabled-by-default checkbox and a `ContextSetInheritance` handler that refreshes the list.
- Cards: `PromptContextAddonCardFactory`. Only the `Enabled` toggle is drawn (prompt contexts have no hidden state); `AddonCardEnabledChange` disables itself for fixed sections and for read-only contexts (`CanEdit = !IsFixed && SetConfig is not null`).
- A section that is on by default needs no configuration at all: `AddonSetConfigurationBase.EnabledByDefault` is `true`, so a chat without an entry in `Changes` gets the section.

## 8. Checklist

1. Quartet + section + provider files under `Providers/<Name>/`.
2. Template `Resources/sections/<section>.llt` with both ids.
3. Localization keys (`prompt.context.loc` iv + ru-RU, `addon.type.context.*` already exist for the kind).
4. `Description` in English on the new `PromptContextInfo` (and on any native section that still says `string.Empty`).
5. `dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj'`, then `dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath='$env:TEMP\dass-temp-build\'`.
6. Live check: the first request after the section appears carries it either in the frozen header or as a `<system-reminder>` delta - read it, it is the section's real output. A section that re-announces everything on every restart means pitfall 5.2.

## Pointers

- Template authoring, `params_schema`, localization of prompt parts: the `llt-prompt-parts` skill.
- `AGENTS.md` -> **Prompting & SCM** for the modes, checkpoints and section list.
- Reference implementation: `Providers/WorkingDirectories/` (custom delta engine, `State = null` handling) and `Providers/Skills/` (addon-backed sections on `AddonSectionDeltaEngine`).
