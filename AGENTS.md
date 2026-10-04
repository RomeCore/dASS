# dASS - Desktop Assistant (AGENTS.md context)

The project is cross-platform C#/Avalonia application for universal LLM/agentic interactions.

## Agents

The project has **2** separate agentic execution systems:

1. *Chat agents* - agents that live in a chat, triggered by user messages, ordered by consecutive *execution stages*, and cannot execute in parallel.
2. *Agentic tasks* - agents that runs once for doing specific tasks
    - Internal system tasks, such as chat naming, automatic memory recording/retrieval, etc.
    - Explicit invokation via `agent-call` tool or via `dass.agent.call` Lua API
    - Predefined *sub-agents* - markdown addons (`agents/<name>.md` / `agents/<name>/AGENT.md`) loaded from addon packs, see **Addons**

### Directories

```
src/LLMDesktopAssistant/Addons/
    AddonBase.cs - base of every agentic addon (name, order, aliases, description/body, paths, diagnostics)
    AddonKind.cs - [Flags] kinds: Pack, Skill, SubAgent, Tool, PromptContext, Template, LuaScript (+ MemoryBlock, Command - reserved)
    IAddonTypeDescriptor.cs - per-kind descriptor marked with [AddonTypeDescriptor]
    AddonSetCollectorBase.cs - merges addons across packs, applies per-agent changes, freezes clones
    Loading/ - AddonFileLocatorBase<T>, pack locators (App/Chat/Combined), AddonPackSearchFoldersProvider
    Management/ - addon managers, invalidators, settings watchers
    Parsers/ - FrontmatterBasedAddonParser + YAML-frontmatter property parsers
    Search/ - BM25 search over the addons available to an agent
    MVVM/ - addon list/card UI (AddonListView, AddonCardView, card factory)
    ... per-kind models and parsers live in their own domain folders, not here (see **Addons**)

src/LLMDesktopAssistant/Agents/
    ChatAgentDescriptor.cs - the descriptor/configuration object for the chat agent
    ChatAgentInstance.cs - the agent reference (by agent's GUID) used in execution stages ONLY
    ... other root files used mostly for ChatAgentDescriptor's configuration
    ExecutionStages/
        AgentExecutionStage.cs - abstract base class for execution stages
        AgentPreExecutionContext.cs - context that taken by execution stages to select next agent
        AdaptiveAgentExecutionStage.cs - the adaptive execution stage that uses LLM-based router for selecting next agent
        ...
    Memory/ - agentic memory-related directory (facts/episodic logs)
        IMemoryFactStore.cs
        IMemoryLogStore.cs
        ...
    SubAgents/ - the sub-agent addon kind: info object, parser, loader, file locator, type descriptor
        SubAgentInfo.cs - the main sub-agent information object, contains name, description, metadata, used tools, skill, memory blocks, inner sub-agents
        ...
    Tasks/
        AgentTask.cs - agentic task object, produced by AgentTaskExecutor
        AgentTaskExecutor.cs - the implementation of IAgentTaskExecutor
        AgentTool.cs - abstract definition of tool, used by agent inside task
        ChatAgentTool.cs - wrapper for chat's ToolInfo that inherits AgentTool
        ...

src/LLMDesktopAssistant/Prompting/
    Context/ - SCM: anchors, deltas, sections, checkpoint kinds (see **Prompting & SCM**)
        Providers/<Name>/ - one folder per section (state/delta provider and renderer quartets)
    Management/ - LLT template importers and prompt part managers (personas, specializations, slots, skills, sub-agents)
    Resources/ - built-in *.llt templates (core_prompt, components, sliders, message_prompt, naming, router, summarizer)
    LLT/ - LLT editor control, tokenizer, diagnostics
    Skills/ - the skill addon kind (SkillInfo, parser, locator, type descriptor)
    ContextCheckpoint.cs, ContextCheckpointKind.cs, SystemPromptSnapshot.cs

src/LLMDesktopAssistant/LLM/
    Domain/
        Chat.cs - the chat instance itself, contains a messages sequence and ContextTabs - point of persisted/visual extension
        ChatMessage.cs - abstract base class for messages
        ...
    Services/
        ChatExecutionService.cs - **central** service for chat execution pipeline
        ...
        Agents/
            AgentManagementService.cs - service for chat agents retrieval
            AgentOrderingService.cs - service for getting next chat agent for execution
            ...
        Prompting/
            AgentPromptComposer.cs - **central** per-agent prompt entry point: builds the (messages + tools) bundle, owns header mode and toolset-cache invalidation
            AgentEffectiveMessagesProvider.cs - effective message set for an agent plus its active checkpoints
            PromptAnchoredSectionProcessor.cs - SCM anchor lifecycle and delta emission
            MessageVisibilityService.cs - per-agent message visibility
            ChatMessageQuoteRenderer.cs - neutral content quote of a message (naming, summarizer, router)
            ...
        Tools/
            ToolExecutionService.cs - service for chat-related tool execution
            ToolsetBuildingService.cs - service for collecting and building toolset from all sources for each agent
            ...

```

## Addons

Skills, sub-agents, tools, prompt contexts, templates and Lua scripts are all *addons*, discovered from *addon packs* on disk. Infra lives in `src/LLMDesktopAssistant/Addons/`; each kind's model, parser and locator live in that kind's domain folder.

A pack is a directory scanned for one folder per kind:

| Kind | Folder in pack | Layout |
|---|---|---|
| `Skill` | `skills/` | `skills/<name>/SKILL.md` or `skills/<name>.md` (`.md`, `.mdx`) |
| `SubAgent` | `agents/` | `agents/<name>/AGENT.md` or `agents/<name>.md` |
| `Tool` | `tools/` | extensions of the registered script engines (`.lua`, `.alua`, `.py`, `.csx`) |
| `LuaScript` | `scripts/lua/` | `.lua`, `.alua` |
| `Template` | `templates/` | LLT template extensions |
| `PromptContext` | `context/` | extensions not implemented yet |

Packs are discovered under the working directories, the user profile and `Directories.AddonPacks`. `AddonPackSearchFoldersProvider` also scans other agent runtimes' home folders (`.agents`, `.claude`, `.gemini`, `.codex`, `.github`, `.cursor`, `.windsurf`, `.opencode`, `.cline`, `.roocode`, `.lmstudio`, `.junie`, `.everywhere`, ...), so third-party skills and sub-agents are picked up as-is - expect addons you did not create.

Adding a kind = `<X>Info : AddonBase<X>` + parser + `IAddonFileLocator<X>` (`Folders`, `Extensions` ordered by priority, `AllowShortFormat`, `FullFormatName`) + `IAddonTypeDescriptor` marked `[AddonTypeDescriptor]` + `AddonSetCollectorBase<X, TChange>`, plus an optional card factory and search provider. `MemoryBlock` and `Command` kinds are declared but reserved.

Addons are **frozen after collection** - `addon.Clone()` returns an unfrozen copy to mutate.

## Prompting & SCM

`ChatPromptBuilder` is gone; the per-agent pipeline is `AgentPromptComposer` (messages + tools bundle, header mode, toolset-cache invalidation) -> `AgentEffectiveMessagesProvider` (effective messages plus active `ContextCheckpoint`s) -> `PromptAnchoredSectionProcessor` (SCM stage) -> message conversion and hooks. Visibility lives in `MessageVisibilityService`, neutral message quoting in `ChatMessageQuoteRenderer`.

SCM (Sequential Context Management) keeps the prompt prefix **byte-stable** and reports state changes as **events in the message stream** instead of silently rewriting the prompt. Mode is per-agent: `ChatAgentDescriptor.Context.PromptMode` (`AgentContextSettings`), default `Hybrid`.

| Mode | System prompt source | Change tracking |
|---|---|---|
| `Dynamic` | rebuilt on every request | none |
| `Static` | frozen in agent config until manual refresh | none |
| `Hybrid` | bytes blitted from a `PromptStateAnchorMessageData` in the history | anchors + deltas + rebaseline |

In `Hybrid` a live anchor pins rendered bytes and section states to a message; on change, the delta is attached to the agent's own pending assistant message; any `ContextCheckpoint` cut newer than the anchor forces a rebaseline. History is append-only - dead anchors stay inert and are never deleted - and everything is per-agent (`AgentId`). `MaxVisibleRounds` is deliberately ignored in `Hybrid`, since a round window would evict the anchor.

A *section* is one tracked unit of prompt state (identity, system slot, system reminder, tools, skills, sub-agents, memory blocks), implemented as a `state provider + state renderer + delta provider + delta renderer` quartet under `Prompting/Context/Providers/<Name>/`. Addon-backed sections share `AddonSectionDeltaEngine`: a new item is announced in full, an unchanged one by name, a changed one field-by-field, and hiding an item is silent.

Checkpoints are `ContextCheckpoint` carrying the `[Flags]` kind `ContextCheckpointKind` (`Shield`, `Summary`, `ToolCompaction`, `ForcedToolCompaction`, `ReasoningCompaction`). `AgentContextSettings` carries `PromptMode`, `MaxVisibleRounds`, `DisabledFlags`, `Snapshot` and `ContextSet`.

## Dependency Injection

Project uses Microsoft.Extensions.DependencyInjection, paired with own reflection-based registration system. It uses three main scopes:
- `App` - registered with `LLMDesktopAssistant.Services.ServiceAttribute(Type? serviceType = null)` as a singleton, multiple attributes are allowed to register under multiple service types.
- `Chat` - registered with `LLMDesktopAssistant.LLM.Services.ChatServiceAttribute(Type? serviceType = null)` as a scoped type, where single scope = single chat, multiple attributes are also allowed. All app services are avaliable within chat services.
- `WebUI` - registered with `LLMDesktopAssistant.Blazor.Services.WebUIServiceAttribute(Type? serviceType = null, IsScoped = true|false)` as service within ASP.NET web application (scoping is optional). Multiple attributes are not allowed here. All chat services (including app) available for WebUI services.

Sugar attributes:
- `LLMDesktopAssistant.Tools.ToolModuleAttribute(bool chatScoped = true)` used to define inheritants of `LLMDesktopAssistant.Tools.ToolModule` as services. Sugar for `[Chat]Service(typeof(ToolModule))`
- `LLMDesktopAssistant.Scripting.Lua.LuaApiAttribute(bool chatScoped = true)` used to define inheritants of `LLMDesktopAssistant.Scripting.Lua.LuaApiBaseAsync` as services. Sugar for `[Chat]Service(typeof(LuaApiBaseAsync))`

Examples of registering service:
```csharp
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Agents.Tasks
{
	[Service(typeof(IAgentTaskExecutor))]
	public class AgentTaskExecutor : IAgentTaskExecutor
	{
        ...
    }
}
```

```csharp
namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	[ToolModule] // Chat-scoped by default
	public class FilesystemToolModule : ToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IDocumentReadingService _documentReader;

		public FilesystemToolModule(IWorkingDirectoryAccessService fileAccess, IDocumentReadingService documentReader)
        {
            ...
        }

        ...
    }
}
```

### Service configurators

You can define a class that inherits `LLMDesktopAssistant.Services.ServiceConfigurator`. Also put `LLMDesktopAssistant.Services.ServiceConfiguratorAttribute(ServiceScope scope = ServiceScope.App)` attribute on top of it, for example:

```csharp
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;

[ServiceConfigurator(ServiceScope.App)]
public class AppToolModulesConfigurator : ServiceConfigurator
{
	public override void Configure(IServiceCollection services)
	{
		var toolModules = ReflectionUtility.GetTypesWithAttribute<ToolModule, ToolModuleAttribute>();
		foreach (var toolModule in toolModules)
		{
			if (!toolModule.Attribute.ChatScoped)
				services.AddSingleton(typeof(ToolModule), toolModule.Type);
		}
	}
}

[ServiceConfigurator(ServiceScope.Chat)]
public class ChatToolModulesConfigurator : ServiceConfigurator
{
	public override void Configure(IServiceCollection services)
	{
		var toolModules = ReflectionUtility.GetTypesWithAttribute<ToolModule, ToolModuleAttribute>();
		foreach (var toolModule in toolModules)
		{
			if (toolModule.Attribute.ChatScoped)
				services.AddScoped(typeof(ToolModule), toolModule.Type); // Note that chat services are scoped!
		}
	}
}
```

## Localization

Project uses own localization system based on .loc files:

```
// Comment; only exact single line comments are supported, lines that not starts with '//' cannot contain comments

// Locale definition, leave empty for 'iv' (invariant locale)
%locale:

// Namespace, used as prefix for all locale keys in locale file
%namespace: model

// For example, this locale key is 'model.capability.chat'
capability.chat: Chat completions
```

Localization files are located in `src/LLMDesktopAssistant/Localization/Resources/*locale*/*.loc` as an embedded resources and auto-imported by `LLMDesktopAssistant.Localization.LocFileLocalizationManager` (do not worry about that, just put .loc files and it will just work!).

**Important note**: when searching for existing locale files using `fs-grep` - DO NOT PUT POSSIBLE NAMESPACE AS SEARCH PATTERN - or tool will not find anything (namespace is located separately). Example: when searching for `model.capability.chat`, search for `capability.chat` instead. Also notice: file name != namespace (possibly, e.g. tools.loc has `tool` namespace).

### How to use locale keys

Use `LLMDesktopAssistant.Localization.Locale` static facade class for getting locale keys and values:

```csharp
using LLMDesktopAssistant.Localization;

LocaleKeyBase dynamicKey = Locale.GetKey("tool.name.fs-edit");
LocaleKeyBase constKey = Locale.GetConstKey("Edit file"); // Get wrapper around LocaleKeyBase that returns static string without localization

// Three ways to get SAME value
string localized = Locale.Get("tool.name.fs-edit");
localized = dynamicKey.Value;
localized = dynamicKey.RawValue ?? "tool.name.fs-edit";
localized = constKey.Value;
```

Inside AXAML (via `LocExtension`):

```xml
<...
    xmlns:loc="using:LLMDesktopAssistant.Localization"
    ...>

    <!-- Use static string key -->
    <Button Content="{loc:Loc common.save}"/>

    <!-- Use reactive binding to LocaleKeyBase -->
    <TextBlock Text="{loc:Loc {Binding TitleKey}}"/>

</...>
```

## Settings system

Settings are managed by `LLMDesktopAssistant.Settings.SettingsManager` using `SettingsCategory<TObject>` where `TObject : SettingsObject`. Settings are auto-saved on even *deep* changes (using `LLMDesktopAssistant.Utils.ChangeTracker`), and serialized using `System.Text.Json` with string enum conversion and own abstract type resolution (via `LLMDesktopAssistant.Utils.Json.JsonDerivedAttribute(Type baseType, string discriminator)` on implementation/derived types). `SettingsAttribute(string name)` is used to define name for JSON file that will contain configuration.

### Accessing app & chat settings

For accessing app settings - use `LLMDesktopAssistant.Settings.Application.ApplicationSettingsAccessor`, it has `ApplicationSettings` property that returns `LLMDesktopAssistant.Settings.Application.ApplicationSettings`. **Always** use accessor - its good for testing purposes.

For chat settings - inject `LLMDesktopAssistant.LLM.Services.IChatSettingsService`, it has `LLMDesktopAssistant.LLM.Settings.ChatSettings Settings` property.

### Chat & Agent settings inheritance

`ChatSettings` and `AgentDescriptor` can have inherited settings inside each category. `Chat` settings can be inherited from `App` settings, and `Agent` settings can inherit from both. Source generator generates for each "inherited" setting that have `LLMDesktopAssistant.SourceGenerators.InheritedChatSettingAttribute` (for `Chat`) and `LLMDesktopAssistant.SourceGenerators.InheritedChatAgentSettingAttribute` (for `Agent`). Each category must have `SettingsRouteAttribute(string route)` for enable generation. Examples:

```csharp
[SettingsRoute(nameof(ChatAgentDescriptor.Read))]
public partial class AgentReadSettings : AgentSettingsCategoryBase
{
	private AgentReadPermissions _readPermissions = ...;
	/// <summary>
	/// The permissions that determine what the agent can read.
	/// </summary>
	[InheritedChatAgentSetting]
	public AgentReadPermissions ReadPermissions
	{
		get => _readPermissions;
		set => SetProperty(ref _readPermissions, value);
	}

    ...
}
```

Generator will generate for each inherited property:
```csharp
partial class AgentReadSettings
{
	public global::LLMDesktopAssistant.LLM.Settings.ChatSettingsInheritanceLevel ReadPermissionsInheritance { get; set; }

	public global::LLMDesktopAssistant.Agents.AgentReadPermissions GetEffectiveReadPermissions(global::LLMDesktopAssistant.LLM.Settings.ChatSettings chatSettings)
	{
		...
	}

	public void SetEffectiveReadPermissions(global::LLMDesktopAssistant.LLM.Settings.ChatSettings chatSettings, global::LLMDesktopAssistant.Agents.AgentReadPermissions value)
	{
		...
	}
}
```

`Chat` settings have same generated methods signature, except for removed `chatSettings` parameter.

### Using inherited settings

```csharp
ChatAgentDescriptor agent = ...;
ChatSettings chatSettings = ...; 
var readPerms = agent.Read.GetEffectiveReadPermissions(chatSettings);

var skillSources = chatSettings.Skills.GetEffectiveSources();

chatSettings.Skills.PropertyChanged += (s, e)
{
    if (e.PropertyName == nameof(ChatSkillSettings.SourcesInheritance))
    {
        // Catch inheritance level changes
    }
};
```

Note: `ChatSettingsInheritanceLevel` enum have `{ Application, Profile, Agent }` values.

## MVVM

For most ViewModels this project uses `LLMDesktopAssistant.MVVM.ViewModelBase` and `LLMDesktopAssistant.NotifyPropertyChanged` (parent for `ViewModelBase`) classes. Main usage pattern: `ViewModelBase` used for ViewModels, and `NotifyPropertyChanged` used for parts/items inside ViewModels. Project uses reflection-base view locator, VM's can be bound to views used `LLMDesktopAssistant.MVVM.ViewModelForAttribute(Type targetView)`:

```csharp
using LLMDesktopAssistant.MVVM;

[ViewModelFor(typeof(SomeView))]
public class SomeViewModel : ViewModelBase
{
    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
                RaisePropertyChanged(nameof(EnabledChanged)); // Optional: raise changed event on depended property
        }
    }

    private RangeObjservableCollection<int> _ids = [];
    public RangeObjservableCollection<int> Ids
    {
        get => _ids;
        set => _ids.Reset(value); // Reset the collection with new items instead of setting the entire property
    }
}
```

### Dialogs

You can use `LLMDesktopAssistant.Controls.Dialogs.DialogManager` for showing dialogs:

```csharp
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;

var dialog = new ConfirmDialogViewModel
{
	Title = LocalizationManager.LocalizeStatic("settings.memory.delete.title"),
	Description = LocalizationManager.LocalizeStatic("settings.memory.delete.confirm"),
	ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.delete.action"),
	CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
	IsDanger = true
};

var result = await DialogManager.ShowDialogAsync(dialog);
var confirmed = (bool)result!;

// Or inside the dialog's VM
DialogManager.CloseDialog(true);
```

## Useful utilities

These are located in `LLMDesktopAssistant.Utils`:

- `RangeObservableCollection` - thread-safe alternative to `ObservableCollection` that can be used for settings and MVVM, is NEVER raises `CollectionChanged` event without `OldItems` and `Newitems`, which is convenient when `Reset` event is unwanted to deal with. **Important notice**: use `Reset(IEnumerable<T>)` method for setter inside reactive objects to reset the *collection* instead of resetting entire *property*.
- `ReadOnlyObservableCollection` - wrapper around any of `IReadOnlyList<T>`, `INotifyCollectionChanged` or `INotifyPropertyChanged` (every implementaion is optional, but at least on must be implemented).
- `AsyncCache` - thread-safe async dictionary with cleanup intervals and sliding expiration time.
- `ChangeTracker` - deep tracker for one specific reactive object, use `ChangeTracker.Untracked` attribute on properties to prevent deep observation for complex objects (`Task` for example).
- `LLMDesktopAssistant.Disposable` - base class for all disposable objects, you can create direct instance of it with provided `Action` (e.g. `new Disposable(() => ...)`).

## Important notes

The info given in this context document is ACTUAL and you are not needed to observe files by self (unless REALLY needed), even if you miss the right signatures, the `dotnet build` will show all the errors - it's better "invent" signatures and fix errors later, because you will spend less tokens!

So, the GOOD example:
```
Okay, I will just use `GetEffectiveReadPermissions(_chatSettings.ChatSettings)`, i don't need to observe the real generator and spend input tokens + one request.
```

The BAD example:
```
Let's view the `src/LLMDesktopAssistant.SourceGenerators/InheritedSettingsGenerator.cs`, who knows if user is right?
*fs-read_entry call*
The user was right, it works as he said...
```

Also, observe the MINIMAL number of files that really needed for understanding behaviour and signatures.

## Building project

DO NOT build the entire solution! Build the each project separately instead (but desktop project should be built inside the temporary directory):

```powershell
dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj'
dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath='$env:TEMP\dass-temp-build\'
```

**NEVER pipe the build output through `Select-Object` (`| Select-Object -Last 40`, `| Select`, `| select`, etc.)!**
Do not filter, truncate or post-process the build output in any way - run the command bare and let it stream.
Reasons:
- it breaks the output encoding (the Russian text in the build log turns into mojibake);
- it kills the streaming/animation of the shell tool, which makes `runTerminal: true` essentially useless (no live progress, output appears only when the command finishes).
If the output is too long - just read the part you need from the streamed result, never by piping it.

## Project memory

Use specified memory block to store completed work inside the epizodic logs after the completed session. You also can get last 5-10 logs to view the actual state of the project.