# Slash commands — v1 spec

> Assembled from the wayfinder map [`map.md`](./map.md). Every statement traces to a resolved ticket in [`issues/`](./issues/); tickets and prototypes are the source of truth.

## 1. Scope

### 1.1 In scope (v1)

- The **command engine**: the `SlashCommand` addon kind, its registry, providers and resolver.
- Two command sources: **skill commands** (`/skill:<name>`) and **sub-agent commands** (`/agent:<name>`).
- The **message-insertion / execution service** and the multi-level **execution token**.
- The **command fingerprint** and the **disabled-message toggle**.
- The input UX: **autocomplete popup** and **highlighting / argument completion**.
- The **Lua API** (`dass.commands.*`).
- Chat-level **sub-agent tool policies** (required by `/agent:*`).

### 1.2 Non-goals / fog

Native (built-in) commands; scriptable commands and their script-side API; `.md`/`.mdx` command files; derived commands beyond skill/agent; command composition/pipelines; interactive/awaiting commands; Blazor WebUI; RPC; per-command permissions/policy; localization specifics; activating `ModelFacingMode` `Neutral`/`Hidden`; tools-v2 argument schema; structured results in the fingerprint; grouping the popup by type.

### 1.3 Out of scope

`ToolExecutionService` decomposition; implementation/build; a broader chat-control Lua API (`dass.chat.*`).

## 2. Command model (settled premises)

- A command is a **layer over the chat**: a token at the very start of a message that triggers a runtime action. **One message carries zero or one command**; the whole remainder of the text is that command's arguments.
- A command **always creates a message** (`ChatMessage`, not `UserMessage`/`AssistantMessage`).
- Execution happens **after** the message is inserted; the chat's execution token is set and used as the command's **cancellation token**.
- **Intent ceiling**: the user's send button is a `bool` generate-intent *input*; the command resolves a `bool` generate *outcome*; the final decision may be **lowered by the command but never raised**. A command's own work (sub-agent run, skill injection) is **not** "generation" and is not gated by the button.
- Commands are **user-only**: never exposed to LLM agents. They are invokable from the UI and the Lua API.
- Availability is **chat-level and agent-agnostic**.
- In v1 the agent sees the **raw** message content; a machine-readable fingerprint is written and carries `ModelFacingMode`.

## 3. Command kind, registry and sources

- `SlashCommandInfo : AddonChangedBase<SlashCommandInfo, SlashCommandChange>` — a real addon info type. `AddonKind.Command` is **renamed to `SlashCommand`** and activated.
- Fields: `Namespaces : ImmutableList<string>` (ordered; the bare name is `AddonBase.Name`), `ModelFacingMode : ModelFacingMode` (`Raw | Neutral | Hidden`, default `Raw`), `Generate : bool?` (default `null` = leave the intent untouched), `ICommandExecutor Executor` (required), `SlashCommandArgumentSchema` (§5).
- `ICommandExecutor`:

```csharp
Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct);
// ctx: Chat, Message (target, already inserted), Positionals, Keyed, RawPositionalArguments, RawArguments,
//      GenerateIntent, Services
// result: bool Generate, string? Error
```

  The executor **mutates the target message directly** through `ctx`; the host owns insertion, the token and the hand-off to generation.

- **Collection**: `SlashCommandSetCollector : AddonSetCollectorBase<SlashCommandInfo, SlashCommandChange>`, `[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]`.
  - v1 sources are services registered as **`ISlashCommandProvider`**, pulled through `GetAdditionalAddons()`.
  - v1 providers: **skills** and **sub-agents**. A derived command is produced from each kind's `GetAvailableAddons()` — *all* addons of the kind, **not** filtered by per-chat/per-agent enablement.
  - `AddonSetCollectorBase` gains a `virtual` deduplication key: `GroupBy(s => s.Name)` → `GroupBy(GetDeduplicationKey)` (default `Name`).
- **Enablement**: all commands are enabled by default; a derived command does **not** inherit the source's effective `Enabled` (`Enabled`/`Hidden` zeroed). Some native commands may set `IsFixed = true`. `Hidden` is not used. Master gate: `SlashCommandSet : AddonSetConfigurationBase` under a chat-level `EnableCommands`.
- **Caching**: none in v1 — delegate to the collectors.

## 4. Resolution and namespacing

- A command's **namespaces** = its **type** and, when it comes from a pack, its **pack** (`SourcePack.Name`). v1 types: `skill`, `agent`; reserved: `tool`, `script`.
- **Token grammar**: `:`-separated; the **last** segment is the name, all preceding segments are namespace qualifiers, each must match one of the command's namespaces:
  `/grilling`, `/skill:grilling`, `/matt-pocock:grilling`, `/skill:matt-pocock:grilling`.
- Matching is **case-insensitive**; names are normalized/validated as by the source; `Aliases` resolve on par with `Name`.
- **Conflicts**: a bare `/name` resolves by `OrderBy(OverrideOrder).ThenBy(Order)` (higher `OverrideOrder` wins; the **native > scriptable > derived** tier is encoded there). Losers are reachable **only through a namespace** and are **marked in the UI** (orange + underline). `Ambiguous` is reserved for a true tie.
- `SlashCommandSetCollector` uses the **fully-qualified** dedup key (`type [ + pack] + name`).
- Resolver: chat-scoped `ISlashCommandResolver` over `IAddonSetCollector<SlashCommandInfo>` → `SlashCommandResolution(SlashCommandInfo? Command, LocaleKeyBase? Error)` (`Unknown` / `Ambiguous`).

## 5. Command formats and argument schema

- **Argument grammar** (`/ns:cmd` + argument text; `RawArguments` = the whole remainder):
  - a new **argument tokenizer** (not `ShellCommandSplitter`): whitespace separates, `'…'`/`"…"` group and the quotes are **stripped**, `\` escapes `"` in double quotes; tokens carry a "was quoted" flag;
  - a single left-to-right resolver: `key=…` where `key` ∈ schema `Keyed` starts a keyed argument; an **unquoted** value runs to the next schema key (spaces allowed), a **quoted** value bounds it; a `key=value` with an **undeclared** key stays plain text; everything else is positional;
  - **mixed is allowed** (positionals precede the first key); quoting a keyed value is for **symmetry**, not protection;
  - a **rest positional** (the "big" argument) is delivered raw as `RawPositionalArguments`.
- **Schema**:

```csharp
public class SlashCommandArgumentSchema
{
    public ImmutableList<SlashCommandArgument> Positionals { get; set; } = [];
    public ImmutableDictionary<string, SlashCommandArgument> Keyed { get; set; } = [];
    public bool HasRestPositional { get; set; }
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

- **Format provider** (validation + conversion + completion for one argument):

```csharp
public interface ISlashCommandArgumentFormatProvider
{
    bool TryValidate(string raw, out LocaleKeyBase? error);
    object? Convert(string raw);
    bool CanComplete { get; }
    IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx);
}
```

- **Parsed arguments**: `ParsedSlashCommandArgument { Definition, Raw, Value }`.
- **Validation at send**: an unknown command, or one whose arguments fail `TryValidate`, **blocks the send** (nothing is inserted). `Completer` is UX-only.
- **File / scriptable formats (contracts, not implemented)**: pack folder `commands/`; `SlashCommandParser : FrontmatterBasedAddonParser<SlashCommandInfo, SlashCommandChange>` with descriptor like `SkillParser` and its own thin `Populate` (SkillInfo's body/frontmatter is **not** reused); frontmatter = base set + `namespaces`, `model-facing`, `generate`, `argument-schema`; scriptable `IScriptableCommandEngine` mirrors `IScriptableToolEngine`; file commands arrive via the **non-additional** set (`IAddonAccessor<SlashCommandInfo>`).

## 6. Message insertion and execution

- New chat-scoped **`IChatMessageInsertionService`**; `ChatOperationService` delegates the send path to it.

```csharp
Task<UserInputInsertionResult> TryInsertUserInputAsync(
    UserInput input, bool generateIntent, int? editIndex = null, CancellationToken ct = default);
public readonly record struct UserInputInsertionResult(bool Success, LocaleKeyBase? Error, ChatMessage? Message);
```

- **Execution token** (shared infrastructure):

```csharp
public enum ChatExecutionLevel { None = 0, Operation, Command, AgentSequence, Agent, Message }
public interface IChatExecutionTokenService
{
    CancellationTokenSource? ExecutionCancellationToken { get; }  // alive while any level is; null when the chat is idle
    event Action? ExecutionCancellationTokenChanged;
    IDisposable WithToken(ChatExecutionLevel level, CancellationToken inputCt, out CancellationToken cancellationToken);
    bool TryCancel(ChatExecutionLevel level);
}
```

  Rules: `Operation` is the **widest** level — cancelling it (e.g. switching a branch, any chat mutation) cancels everything (generation, router, commands). Taking level `L` cancels/replaces the `L` token and cascades into all narrower levels; levels may be **skipped** (`Message` without an active `Operation` is fine); each level links the caller's `inputCt` **and** the nearest live wider level; `Dispose` releases `L` and cascades into narrower levels; a narrower level never cancels a wider one. `ExecutionCancellationToken` is created lazily with the first active level and released when the last one is released — its `.Cancel()` kills all levels. `Chat.GenerationCts` is **removed**: the UI's "is generating" flag is `ExecutionCancellationToken != null` and the cancel button calls `ExecutionCancellationToken.Cancel()`. The command runs under `Command`.

- **Flow**:

```
if the leading token is a command:
    resolve → unknown ⇒ block, nothing inserted
    parse + validate args → failure ⇒ block, nothing inserted
build UserMessage (raw Content) → storage.AppendMessage
if a command was resolved: executor.ExecuteAsync(ctx)   // mutates the message; returns bool outcome
finalGenerate = GenerateIntent && outcome
if finalGenerate: ChatExecutionService.GenerateResponseAsync(ct)
```

- **Runtime** errors are attached to the message (`ChatMessage.Error`); commands run only on **new-message** insertion (`editIndex` edits do not re-run them).
- **Error model**: `Error : string?` moves from `AssistantMessage` up to **`ChatMessage`**.

## 7. Skill and agent commands

### `/skill:<name> args…`

- Body injected as a new reusable **`AdditionalMessageContentPart : AdditionalMessagePart`** whose `Content` is appended to `message.Content` inside `ChatMessageQuoteRenderer` (**no context expander**); chip badge, e.g. *"Used skill 'grilling'"*.
- `AdditionalMessagePart` gains **`bool IsRestorable { get; set; } = true`**; command parts set it `false` so editing does not clone them into `UserInputState`.
- Positional arguments are **substituted into the body** (`$ARGUMENTS` / named placeholders).
- Body always injected **in full** (`InjectionMode` is not applied); the `SkillLoadTool` home-directory note is appended when `HomeDirectory` is set.
- Default `Generate = null`.

### `/agent:<name> input`

- Input = `RawPositionalArguments` → `AgentUserMessage.Content`.
- Launch mirrors `agent-callsub`: fresh `AgentTaskLaunchParameters { TaskName, TriggeredChat, TriggeredMessage = commandMessage, InitialMessages = [], AutoApproveBehaviours, DisallowedBehaviours }` → `ISubAgentTaskParamsResolver.Resolve(...)` → `IAgentTaskExecutor.Execute`; the task attaches to `message.AgentTasks`.
- Policies come from **`ChatSubAgentSettings`** (§10).
- **Fire-and-forget**; a named argument **`wait`** (default `false`) can await the result (schema: `Keyed = { wait: bool }` + `HasRestPositional`).
- Default `Generate = null`.

## 8. Command fingerprint

- `SlashCommandFingerprint : AdditionalChatData` — **data-only** (`IsVisible = false`), **persisted** (`IsTemporary = false`); a flat BSON-safe snapshot:

```csharp
class SlashCommandFingerprint : AdditionalChatData
{
    string Token; string CommandName; ImmutableList<string> Namespaces; string? Source;
    string RawText; ImmutableList<string> PositionalArguments; ImmutableDictionary<string,string> KeyedArguments;
    ModelFacingMode ModelFacingMode; bool GenerateIntent; bool GenerateOutcome;
    SlashCommandExecutionStatus Status; string? Error; string? EffectSummary;
}
enum SlashCommandExecutionStatus { Executed, Failed, Cancelled }
```

- Created/owned by the **host** (insertion service); **never rendered into the prompt** — it is a trace. `ModelFacingMode` governs the *message content* rendering (v1: `Raw`).

## 9. Disabled-message toggle

- `bool ChatMessage.IsDisabledForAgents { get; set; } = false` (all message types; persisted; mutable).
- Enforced **first** in `MessageVisibilityService.CheckVisibility` — invisible to **every** agent, **including the message's own sender agent** (the same-agent shortcut is bypassed).
- `MessageVisibility.OnlyUsers` is left untouched.
- SCM is unaffected: a disabled message carrying an anchor/checkpoint still contributes it.
- UI: eye toggle at the bottom of the message (next to "render markdown"); hidden messages are dimmed (opacity ≈ 0.7–0.8).

## 10. Chat-level sub-agent tool policies

- `ChatSubAgentSettings` (route `Chat.Settings.SubAgents`) gains a `ToolPolicyMask Policy` with **`[InheritedChatSetting]`**.
- Consumers: the `/agent:*` executor and `agent-callsub` (`AgentSubAgentTools`, reworked to read it instead of the caller agent's policy).
- UI mirrors the BEHAVIOUR POLICY section of `AgentToolSettingsView(Model)` (`ToolBehaviourMaskItem`, `ISetPolicyMaskFlag`, `GetPolicyMaskState`/`SetPolicyMaskFlag`, generated `GetEffectivePolicy`/`SetEffectivePolicy`).

## 11. Input UX

### 11.1 Autocomplete popup ([ticket 08](./issues/08-autocomplete-popup.md), [prototype](./prototypes/08-autocomplete-popup.md))

- A **`Popup`** (`PlacementTarget` = the `HighlightTextBox`, `Placement = BottomEdgeAlignedLeft`, offsets from `HighlightTextPresenter.GetCaretRectIn`). The TextBox keeps focus; the popup is a passive list fed by the **general autocomplete service** (this is where command/namespace completion happens).
- States: closed; open (fresh `/`); filtering; empty; unknown committed (no popup); argument mode.
- Keys: `↓`/`↑` move; `Enter` accepts **if open**, else sends; `Tab` accepts; `Esc` closes (input untouched); typing filters.
- Accept replaces the fragment with the **fully-qualified** token + trailing space.
- List sorted by **`Order`, then name**; defeated/ambiguous commands marked; ~8 rows + scroll; mouse hover/click.

### 11.2 Highlighting & argument completion ([ticket 09](./issues/09-highlighting-and-argument-completion.md), [prototype](./prototypes/09-highlighting-and-argument-completion.md))

- All colouring is `TextHighlightSpan`s over **raw text**; ghost text is **suffix-only** `RenderedText`.
- Palette: **pink** (known) / **red + underline** (unknown) / **light grey** (args) / **orange + underline** (ambiguous).
- Ghost hint for a required-but-missing argument; the **token**'s ghost completion is consumed **char-by-char with `→`**.
- Argument completion is delegated to `ISlashCommandArgumentFormatProvider.Complete` (sync `IEnumerable`). v1 = the `wait=true|false` choice provider; more providers (files, …) are added to the provider set — the engine is never touched.
- The transform provider is pure rendering and knows nothing about the popup.

## 12. Lua API

- `LuaApiCommands : LuaApiBase`, `Namespace => "dass.commands"`, `[LuaApi(chatScoped: true)]`, with `Manuals`.
- `dass.commands.list()` → `name`, `namespaces`, `description`, `source`, `argument_schema`.
- `await dass.commands.invoke(token, arguments?, generate?, wait_for_generation?)` (**snake_case** params):
  - routed through `IChatMessageInsertionService.TryInsertUserInputAsync`, so a Lua command behaves like a user one (creates a message, validates, blocks on failure);
  - the inserted message is authored by login **`"Script"`**;
  - `arguments` = a **string** (raw) or a **table** (Lua array + hash, e.g. `{ "arg1", key1 = "value1" }`) serialised back into argument text;
  - `generate` (default `false`) = the intent; `wait_for_generation` (default `true`) = await the chat generation.
- Returns `{ success, error, message_index, fingerprint }`.

## 13. Reusable layers surfaced by this effort

- `IChatExecutionTokenService` (multi-level token; replaces manual CTS plumbing and removes `Chat.GenerationCts`).
- `AdditionalMessageContentPart` + `AdditionalMessagePart.IsRestorable`.
- `ChatSubAgentSettings.Policy` (and a fix to `agent-callsub`).
- `AddonSetCollectorBase.GetDeduplicationKey`.

## 14. References

Resolved tickets (in [`issues/`](./issues/)): [Command registry and sources](./issues/01-command-registry-and-sources.md), [Command resolution and namespacing](./issues/02-command-resolution-and-namespacing.md), [Injection paths](./issues/03-injection-paths.md), [Skill- and agent-command semantics](./issues/04-skill-and-agent-command-semantics.md), [Message-insertion and execution service](./issues/05-message-insertion-and-execution-service.md), [Command fingerprint](./issues/06-command-fingerprint.md), [Disabled-message toggle](./issues/07-disabled-message-toggle.md), [Autocomplete popup](./issues/08-autocomplete-popup.md), [Highlighting and argument completion](./issues/09-highlighting-and-argument-completion.md), [Lua API exposure](./issues/10-lua-api-exposure.md), [Command formats and argument schema](./issues/12-command-format-and-argument-schema.md), [Chat-level sub-agent tool policies](./issues/13-chat-level-sub-agent-tool-policies.md).
