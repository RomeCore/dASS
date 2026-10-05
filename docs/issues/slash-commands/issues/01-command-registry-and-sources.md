# Command registry and sources

Status: resolved
Type: grilling
Blocked by:

## Question

Define the command **registry** and its **sources** for v1: the abstraction that enumerates the commands available to one chat from the two v1 sources — skill addons (`/skill:<name>`) and sub-agent addons (`/agent:<name>`).

## Answer

**Model & kind**

- `SlashCommandInfo : AddonChangedBase<SlashCommandInfo, SlashCommandChange>` — a real addon info type. `AddonKind.Command` is **renamed to `SlashCommand` and activated** (no longer "reserved"); no file locator/parser in v1 (commands are native/derived only).
- Fields: `Namespaces : ImmutableList<string>` (ordered; the bare name is `AddonBase.Name`), `ModelFacingMode : ModelFacingMode` (new enum `Raw | Neutral | Hidden`, default `Raw`), `Generate : bool?` (default `null` = leave the intent untouched), `ICommandExecutor Executor` (required).
- `SlashCommandArgumentSchema` is **deferred to ticket 12** ("Command formats and argument schema") — it depends on the command formats.
- The concept `ICommand` is **dropped**; there is only `SlashCommandInfo` (the addon info) + `ICommandExecutor` (its engine).

**Executor**

```csharp
interface ICommandExecutor
{
    Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct);
}
// ctx: Chat, Message (the target message, already inserted), RawArguments, Arguments[],
//      GenerateIntent (bool), Services
// result: bool Generate, string? Error
```

- The executor **mutates the target message directly** through `ctx` (adds `AdditionalChatData` / `AgentTask`), like `ToolInfo.Executor` writes into a `ToolCall`.
- The host (ticket 05) owns insertion, `GenerationCts`, and the hand-off to `ChatExecutionService`.

**Collection & sources**

- `SlashCommandSetCollector : AddonSetCollectorBase<SlashCommandInfo, SlashCommandChange>`, registered `[ChatService(typeof(IAddonSetCollector<SlashCommandInfo>))]`.
- v1 sources are services registered as **`ISlashCommandProvider`**; the collector pulls their commands through `GetAdditionalAddons()` (native + derived). The non-additional set is what `IAddonAccessor<SlashCommandInfo>` yields — i.e. future file-based (`commands/`) commands. Native + derived providers synthesize `SlashCommandInfo` (mirrors the `PromptContextNativeProvider` pattern: build → `Freeze()`).
- v1 providers: **skills** and **sub-agents** (derived kinds). A derived command is produced from each kind's `GetAvailableAddons()` — *all* addons of the kind, **not** filtered by per-chat / per-agent enablement.
- `AddonSetCollectorBase` must gain an **additional dedup key** so `SlashCommandSetCollector` deduplicates commands correctly (name alone is not enough once namespaces exist).

**Availability scope**

- **Chat-level, agent-agnostic** — the input box serves the whole chat. (A per-agent command set is fog.)

**Enablement**

- All commands are **enabled by default**. A derived command does **not** inherit the source addon's effective `Enabled`; `Enabled` / `Hidden` are zeroed (`null`). Some native commands may set `IsFixed = true`.
- `Hidden` is **not used** (same as prompt-context and Lua addons).
- A minimal `SlashCommandSet : AddonSetConfigurationBase<SlashCommandChange>` backs a master `EnableCommands` setting (chat-level, inheritable), with room for per-command overrides by name.

**Caching**

- **None** in v1 — delegate to the collectors (they self-invalidate via `AddonAccessor` / `IAddonManagerInvalidator`). (The user intends to remove `ToolsetCacheService` later and move caching into `AddonSetCollectorBase`.)

## Comments

- Claimed and resolved in the same exchange (grilling rounds 1–2).
- Pre-note raised by the user: `AddonSetCollectorBase` needs the extra dedup key described above.
