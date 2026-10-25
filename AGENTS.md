# dASS - Desktop Assistant

dASS is a cross-platform C#/Avalonia application for LLM and agentic interactions. This repository is
its source: your edits need a rebuild and never affect the running instance.

Two agentic systems coexist: **chat agents** (live in a chat, triggered by user messages, ordered by
*execution stages*, never parallel) and **agentic tasks** (run once: chat naming, memory recording,
`agent-call`, predefined *sub-agents*).

## Build & verify

Do not read source files to learn signatures - write the call, build, and fix the errors. Even when you
have to invent a signature first, that is cheaper than observing.

```powershell
dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj'
$a = Join-Path $env:TEMP 'dass-temp-build'   # $env:TEMP does NOT expand inside single quotes
dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath="$a"
```

Build each project separately, never the whole solution, and **never pipe the build output**
(`| Select-Object -Last 40`): it breaks the console encoding (Cyrillic becomes mojibake) and kills the
live streaming of the shell tool.

## Code style

Tabs for indentation, Allman braces, block-scoped `namespace X { }`. `net10.0` with `Nullable` and
`ImplicitUsings` enabled, `LangVersion=latest`; `LLMDesktopAssistant.Controls.Icons` is a global using.

- Injected dependencies -> primary constructor parameters, used by name, **no backing field**:
  `public class Foo(IBar bar) : IFoo`. Real state -> `private readonly _camelCase = ...`.
  Private `const` -> `camelCase`, no prefix.
- Properties go through the `field` keyword: `set => SetProperty(ref field, value);`. Never hand-write
  backing fields and never use `[ObservableProperty]`.
- Collection expressions `[]` everywhere. `var` when the type is obvious from the right side, an
  explicit type when it is not or when it documents intent.
- `Async` suffix and a trailing `CancellationToken cancellationToken = default` on every
  `Task`-returning method.
- `I`-prefixed interfaces. `#region` only to group one interface's members
  (`#region IList (read-only adapter)`), never to split a class into halves. No `this.`.
- Comments are English one-liners that explain *why*, not *what*.
- Domain suffixes are load-bearing, do not invent synonyms: `*Info` addon model, `*Descriptor` agent
  configuration, `*Instance` GUID reference, `*Service`/`*Provider`/`*Processor` DI behaviour,
  `*Settings` a category carrying `[SettingsRoute]`.
- No user-facing string literals - `Locale` keys and `.loc` resources (see **Localization**). No manual
  `services.AddScoped` in feature code - use the attributes (see **Dependency injection**).
- Do NOT use long dash (—) in both XML-docs, Lua manuals and comments, use short dash (-) instead.

XML docs: every public member gets one, and so does a private member when it needs an explanation or
when the algorithm is non-obvious. `<summary>` is **always multi-line** - never
`<summary>One line.</summary>`, and never hide a real explanation behind a bare `<inheritdoc/>`:

```csharp
/// <summary>
/// The name used to identify the addon for deduplication and agent selection purposes.
/// </summary>
public string Name { get; set; }
```

## Where things live

| Need | Entry point |
|---|---|
| Chat turn pipeline | `LLM/Services/ChatExecutionService.cs` |
| Prompt for one agent | `LLM/Services/Prompting/AgentPromptComposer.cs` -> `AgentEffectiveMessagesProvider` -> `PromptAnchoredSectionProcessor` |
| One SCM section | `Prompting/Context/Providers/<Name>/` (state provider, state renderer, delta provider, delta renderer) |
| Message visibility / quoting | `LLM/Services/Prompting/MessageVisibilityService.cs`, `ChatMessageQuoteRenderer.cs` |
| Toolset for an agent | `LLM/Services/Tools/ToolsetBuildingService.cs` |
| Slash command | `SlashCommands/SlashCommandResolver.cs`, input side in `SlashCommands/Input/` |
| Addon loading | `Addons/Loading/`, per-kind folders in their own domain (`Agents/SubAgents/`, `Prompting/Skills/`, `SlashCommands/`) |
| Chat agent | `Agents/ChatAgentDescriptor.cs` (configuration) vs `Agents/ChatAgentInstance.cs` (a GUID reference held by execution stages) |

### Desktop project

Anything that touches an OS process or a real window lives in `src/LLMDesktopAssistant.Desktop`, not
in the core project: `Execution/` (`ProcessLauncher`, `TerminalRun`, `Terminals/TerminalView`),
`Scripting/ExternalProcess/` (the out-of-process script engines - Python plus the tool, slash-command
and prompt-context flavours), `Scripting/Python/`, `Scripting/Lua/API/LuaApiProcess.cs`,
`ToolModules/` (`shell-execute`, `python-execute`, `DesktopFilesystemToolModule`), `Services/`
(explorer and link openers), `Markdown/MermaidPreviewProvider`.

A feature that shells out, spawns an interpreter or paints a terminal belongs there - the core project
must stay portable (`.Android`, `.Browser`, `.Blazor` builds reference it).

## Localization

`.loc` files are embedded resources under `src/LLMDesktopAssistant/Localization/Resources/<locale>/`
and are auto-imported by `LocFileLocalizationManager` - drop the file in, nothing to register.

```
// Comments are single-line and must start with '//'
%locale:
%namespace: model

// full key is 'model.capability.chat'
capability.chat: Chat completions
```

`%locale:` empty means the invariant `iv` locale; `%namespace:` prefixes every key in the file.

**Trap**: in `fs-grep`, search for the *key body* (`capability.chat`), never for the full
`model.capability.chat` - the namespace is declared separately, so the full key matches nothing. The
file name is not the namespace either (`tools.loc` holds the `tool` namespace).

```csharp
LocaleKeyBase dynamicKey = Locale.GetKey("tool.name.fs-edit");   // localized lazily
LocaleKeyBase constKey = Locale.GetConstKey("Edit file");        // static text, no lookup
string text = Locale.Get("tool.name.fs-edit");
```

```xml
<Button Content="{loc:Loc common.save}"/>          <!-- static key -->
<TextBlock Text="{loc:Loc {Binding TitleKey}}"/>   <!-- reactive LocaleKeyBase -->
```

## Settings

`SettingsManager` works with `SettingsCategory<TObject>` where `TObject : SettingsObject`. A category
carries `[SettingsAttribute("file-name")]` for its JSON file, is serialized with `System.Text.Json`
(string enums, own derived-type resolution through `[JsonDerivedAttribute(typeof(Base), "discriminator")]`)
and auto-saves on even deep changes via `ChangeTracker` - mark properties it must not walk with
`[ChangeTracker.Untracked]` (complex objects, `Task`).

- App settings: inject `ApplicationSettingsAccessor` and use its `ApplicationSettings`. **Always** the
  accessor, never a fresh instance - it is the seam tests replace.
- Chat settings: inject `IChatSettingsService`, use its `.Settings`.

### Inheritance

`Chat` settings inherit from `Application`, `Agent` settings from both levels. Every category needs
`[SettingsRoute(nameof(Route))]`, every inheritable property needs `[InheritedChatSetting]` (Chat) or
`[InheritedChatAgentSetting]` (Agent). The generator then adds `<Prop>Inheritance`
(`ChatSettingsInheritanceLevel`: `Application` | `Profile` | `Agent`), `GetEffective<Prop>(chatSettings)`
and `SetEffective<Prop>(chatSettings, value)` - the Chat-side getters take no argument.

```csharp
var readPerms = agent.Read.GetEffectiveReadPermissions(chatSettings.Settings);
var skillSources = chatSettings.Skills.GetEffectiveSources();
```

Read the *effective* value, and set through the inheritance level: assigning the raw property while
the level is still `Application` is silently ignored by the getter.

## Addons

Skills, sub-agents, tools, prompt contexts, templates, Lua scripts and slash commands are all *addons*,
discovered from *addon packs* on disk. Infra is in `src/LLMDesktopAssistant/Addons/`; each kind's model,
parser and locator live in that kind's domain folder. A pack is a directory scanned for one folder per
kind:

| Kind | Folder | Layout |
|---|---|---|
| `Skill` | `skills/` | `skills/<name>/SKILL.md` or `skills/<name>.md` (`.md`, `.mdx`) |
| `SubAgent` | `agents/` | `agents/<name>/AGENT.md` or `agents/<name>.md` |
| `Tool` | `tools/` | extensions of the registered script engines (`.lua`, `.alua`, `.py`) |
| `LuaScript` | `scripts/lua/` | `.lua`, `.alua` |
| `Template` | `templates/` | LLT template extensions |
| `PromptContext` | `context/` | `context/<name>.lua` (`.alua`, `.py`) - one script per context |
| `SlashCommand` | `commands/` | `commands/<name>.<ext>` - a single file, no folder form |

Packs are discovered under the working directories, the user profile and `Directories.AddonPacks`, plus
other runtimes' home folders (`.agents`, `.claude`, `.gemini`, `.codex`, `.github`, `.cursor`, ...) -
**expect addons you did not create**.

**Trap**: addons are **frozen after collection**; mutating one throws. `addon.Clone()` returns an
unfrozen copy to change.

Adding a kind = `<X>Info : AddonBase<X>` + parser + `IAddonFileLocator<X>` (`Folders`, `Extensions`,
`AllowShortFormat`, `FullFormatName`) + `IAddonTypeDescriptor` marked `[AddonTypeDescriptor]` +
`AddonSetCollectorBase<X, TChange>`, plus an optional card factory and search provider. `MemoryBlock`
is declared but reserved.

## Slash commands

A slash command is a **user-only** token at the very start of a message (`/skill:grilling`,
`/agent:web-searcher <input>`, `/tool:fs-read path=README.md`) that runs a runtime action before the
message reaches the model. An agent cannot run one and does not need to: skills, sub-agents and tools
are reachable directly. What it *can* do is read the catalogue served to `addon-search` /
`addon-list_available` / `addon-info`, so it quotes the exact token instead of inventing one.

Three sources, mirrored by `SlashCommandOrderTiers`: **native** `2000` (built in code) > **scriptable**
`1000` (files in `commands/`) > **derived** `0` (one per skill, sub-agent and tool).

1. `SlashCommandExtractor` is the **single owner of the `/` marker**; everything downstream is
   slash-free (`//` escapes a literal leading slash).
2. `SlashCommandResolver` + `SlashCommandMatcher` - exact match, total order is exact name > alias,
   then `OverrideOrder` desc, `Order` asc, `Key`.
3. Arguments: the schema lives on the **executor** (`ISlashCommandExecutor.ArgumentSchema`);
   `SlashCommandArgumentParser` + `SlashCommandArgumentBinder` apply defaults and validate. An invalid
   set blocks the send.
4. Execute under a `ChatExecutionLevel.Command` token - the executor **mutates `ctx.Message` directly**.
   `SlashCommandIntent.Resolve(generateIntent, command.Generate, result.Generate)` can only *lower* the
   caller's intent.
5. `SlashCommandFingerprint` goes into `AdditionalData`; `MessageContentProjector` shapes the model-facing
   content by `ModelFacingMode` - `Raw` (default) sends the content as typed, `Neutral` replaces it with
   the bare `/<token>`, `Hidden` drops it - plus whatever `AdditionalMessageContentPart`s the executor added.

The three derived providers read `GetAvailableAddons()`, deliberately **not** the agent's toolset: for
`/tool:<name>` that is the chat's whole tool surface, so a tool can be runnable by hand while no agent
may call it. `ToolCommandExecutor` builds the `ToolCall` the model would have emitted, attaches it to the
command's own user message and runs it with `ToolInvocationSource.Command` - no policy, no confirmation -
then returns `generate: false`. Its arguments are keyed-only (`/tool:fs-read path=README.md`), mapped from
the tool's JSON Schema by `ToolArgumentSchemaMapper` (unknown types degrade to an unvalidated string).
`SkillCommandExecutor` injects the skill body in full, expanded through `SlashCommandVariableExpander`
(`$ARGUMENTS`, `$SKILL_NAME`, `$SKILL_DIR`); `SubAgentCommandExecutor` runs like `agent-callsub` with a
`wait` argument (default `true`).

Settings: `ChatCommandSettings.EnableCommands` is a chat-local master gate (never inherited),
`CommandsSet` holds per-command overrides keyed by `SlashCommandInfo.Key`.

## Prompting & SCM

The per-agent pipeline is `AgentPromptComposer` (messages + tools bundle, header mode, toolset-cache
invalidation) -> `AgentEffectiveMessagesProvider` (effective messages plus active `ContextCheckpoint`s)
-> `PromptAnchoredSectionProcessor` (SCM stage) -> message conversion and hooks.

SCM keeps the prompt prefix **byte-stable** and reports state changes as **events in the message
stream** instead of silently rewriting the prompt. Mode is per-agent: `ChatAgentDescriptor.Context.PromptMode`.

| Mode | System prompt source | Change tracking |
|---|---|---|
| `Dynamic` | rebuilt on every request | none |
| `Static` | frozen in agent config until manual refresh | none |
| `Hybrid` | bytes blitted from a `PromptStateAnchorMessageData` in history | anchors + deltas + rebaseline |

**Traps in `Hybrid`**: history is append-only, so dead anchors stay inert and are never deleted; a
`ContextCheckpoint` cut newer than the anchor forces a rebaseline; everything is per-agent (`AgentId`);
`MaxVisibleRounds` is deliberately ignored, since a round window would evict the anchor.

A *section* is one tracked unit of prompt state, implemented as a state provider + state renderer +
delta provider + delta renderer quartet under `Prompting/Context/Providers/<Name>/`. Addon-backed
sections share `AddonSectionDeltaEngine`: a new item is announced in full, an unchanged one by name, a
changed one field-by-field, and hiding an item is silent.

`ContextCheckpoint` carries the `[Flags] ContextCheckpointKind` (`Shield`, `Summary`, `ToolCompaction`,
`ForcedToolCompaction`, `ReasoningCompaction`). `AgentContextSettings` carries `PromptMode`,
`MaxVisibleRounds`, `DisabledFlags`, `Snapshot` and `ContextSet`.

### Scriptable prompt contexts

A prompt context addon can be a **script**: `context/<name>.lua|.alua|.py` with YAML frontmatter
(`--[[ ]]` / `"""` - the engine's descriptor owns the markers). Native sections are `anchored`; a script
is `kind:` `live-tail` (default) or `supersede`:

| Kind | Receives | Text lands |
|---|---|---|
| `live-tail` | - | this request only (`<system-reminder>` next to the pending assistant) |
| `supersede` | `previous` = previously injected text | a persisted stamp, re-injected when the old one goes stale |

The contract is one line: stdout is the context text, empty/whitespace means "no context", and the
engine never compares - the provider does, byte-exact. Text is wrapped as `<name>\n...\n</name>` (a
digit-leading name gets an `_` prefix). `timeout:` defaults to 2000 ms with a 10 s ceiling; a timeout or
an error is a log warning and an absent section, never a broken request. Scripts get **no chat API and
no UI** - they are prompt content, not actions. Discriminators are `script:<name>`, so a script can never
shadow a native section. The parsed addon is cached app-wide, so providers must resolve chat services
from `EffectiveChatContext.Services` instead of capturing them.

## Dependency injection

Microsoft.Extensions.DependencyInjection plus a reflection-based registration of our own, in three scopes:

| Scope | Attribute | Lifetime |
|---|---|---|
| App | `[Service]` (`ServiceAttribute(Type? serviceType = null)`, repeatable) | singleton |
| Chat | `[ChatService]` (`ChatServiceAttribute`, repeatable) | scoped, one scope = one chat |
| WebUI | `[WebUIService(IsScoped = true)]` (not repeatable) | ASP.NET web app |

All App services are visible in Chat, all Chat (and App) services in WebUI. Sugar: `[ToolModule]` for
`ToolModule` inheritors and `[LuaApi]` for `LuaApiBaseAsync` inheritors, both `chatScoped = true` by default.

```csharp
[Service(typeof(IAgentTaskExecutor))]
public class AgentTaskExecutor(/* injected deps */) : IAgentTaskExecutor
{
	...
}
```

A `ServiceConfigurator` subclass with `[ServiceConfigurator(ServiceScope.App)]` or
`[ServiceConfigurator(ServiceScope.Chat)]` registers a family of types at once (that is how tool modules
are split into chat-scoped and singleton); remember that **chat services are scoped**, so `AddScoped`
there, not `AddSingleton`.

## MVVM

`ViewModelBase` for view models (`NotifyPropertyChanged` for parts/items inside one), reflection-based
view location via `[ViewModelFor(typeof(SomeView))]`.

```csharp
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
				RaisePropertyChanged(nameof(EnabledChanged));
		}
	}

	private RangeObservableCollection<int> _ids = [];
	public RangeObservableCollection<int> Ids
	{
		get => _ids;
		set => _ids.Reset(value);   // Reset the collection, don't replace the property
	}
}
```

Dialogs go through `DialogManager.ShowDialogAsync(vm)` / `DialogManager.CloseDialog(result)` with
`ConfirmDialogViewModel` (`Title`, `Description`, `ConfirmText`, `CancelText`, `IsDanger`) and localized
strings from `LocalizationManager.LocalizeStatic`.

## Utilities (`LLMDesktopAssistant.Utils`)

- `RangeObservableCollection` - thread-safe `ObservableCollection` that always reports `OldItems` **and**
  `NewItems`. Use `Reset(IEnumerable<T>)` in setters to reset the collection, not the property.
- `ReadOnlyObservableCollection` - wrapper over any `IReadOnlyList<T>` / `INotifyCollectionChanged` /
  `INotifyPropertyChanged`.
- `AsyncCache` - thread-safe async dictionary with cleanup intervals and sliding expiration.
- `ChangeTracker` - deep tracker for one reactive object; `[ChangeTracker.Untracked]` opts properties out.
- `Disposable` - base for disposable objects or a direct `new Disposable(() => ...)`.

## Project memory

Store finished work in this project's episodic log after a completed session, and read the last 5-10
logs to learn the current state before starting.

## Handoffs

Handoff documents live in `.llmassist/handoffs/`, one file per completed phase, named
`<YYYY-MM-DD>-<effort-slug>-<phase>.md` (e.g. `.llmassist/handoffs/2026-10-17-tool-execution-v2-p3.md`).
The `/handoff` skill writes there - not to the OS temp directory. Older one-off handoffs may sit
directly under `.llmassist/`; find the latest with `fs-glob '.llmassist/**/*handoff*'`.

When continuing a multi-session effort, read the latest handoff first: it points to the spec, plan,
tickets and commits instead of repeating them.

## Docs map

- `GLOSSARY.md` at the repo root - domain vocabulary; ADRs in `docs/adr/`.
- `docs/issues/` - the local markdown issue tracker (`docs/issues/README.md`); canonical `Status:`
  values are `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`.
- `docs/ideas/` - idea-and-proposal tickets per scope; `docs/help/` - user-facing help, embedded as resources.
- `docs/agent-reports/` - filed reports about dASS misbehaving (the `/dass-report` skill).
