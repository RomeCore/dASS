# Slash commands — wayfinder map

`wayfinder:map` (the file name itself is the marker; there is no label system).

> **Complete.** All 13 tickets resolved; the destination spec is assembled at [`spec.md`](./spec.md).

## Destination

A locked, implementable **spec** for slash commands **v1** — published as `docs/issues/slash-commands/spec.md` — covering:

- the command **engine + registry** and its two v1 sources (`/skill:<name>`, `/agent:<name>`);
- the chat **message-insertion / execution service** that hosts command dispatch;
- the **"disabled message"** toggle;
- the input **UX**: syntax highlighting + autocomplete in `HighlightTextBox`.

The map ends at the spec. Implementation is a separate effort.

## Notes

**Domain / skills to consult**

- `grilling`, `domain-modeling` (defaults), `codebase-design` for the engine seams; the Avalonia skills for the input-box UX.
- Repo conventions: `AGENTS.md`. Addons live in *packs*; an addon kind = `Info` + parser + `IAddonFileLocator` + `[AddonTypeDescriptor]` + collector; addons are **frozen after collection**.

**Standing preferences for this effort**

- **Never mention competitors** (Claude Code, opencode, …) anywhere in `docs/issues/` or the spec.
- Commands are **user-only**: never exposed to LLM agents (no tool, absent from every agent toolset). UI and Lua API may invoke them.
- A command's target is a **chat message** (`ChatMessage` — not `UserMessage` / `AssistantMessage`; the tool-execution path already had to broaden to `ChatMessage`).
- v1 = **engine + two command sources only**; no native built-ins, no scriptable commands, no `commands/` files.

**Settled premises (decided with the user during charting)**

- A command is a **layer over the chat** — not a new message type. **One message carries zero or one command**; the whole remainder of the text is that command's arguments.
- A command **always creates a message**. Execution happens **after** the message is inserted; the chat's `GenerationCts` is set and reused as the **command's cancellation token**.
- Effects attach to the produced message: `/skill:*` injects the skill body as an `AdditionalChatData` part; `/agent:*` spawns an `AgentTask` on the message. Injecting into AdditionalData is an **executor's** choice, not mandatory for every command.
- **Intent ceiling.** The user's send button is a `bool` generate-intent *input*; the command resolves a `bool` generate *outcome*; the final decision may be **lowered by the command but never raised**. The command's own work (running a sub-agent, etc.) is **not** "generation" and is not gated by the button.
- In v1 the agent sees the **raw** message content; a machine-readable trace ("command fingerprint") is written, and `ModelFacingMode` lives **in that fingerprint**.
- **Namespacing** is optional when there is no conflict; source priority is **native > scriptable > derived**; a pack can contribute a namespace; a command carries a *set* of namespaces (type + pack).
- Pipeline: user sends a command message → an intermediate message-insertion service → (optionally) `ChatExecutionService` for agent-side generation.

## Decisions so far

- [Spec assembly](issues/11-spec-assembly.md): assembled the destination [`spec.md`](../spec.md) from all resolved tickets.
- [Highlighting and argument completion](issues/09-highlighting-and-argument-completion.md) (prototype): spans over raw text only (ghost = suffix-only `RenderedText`); palette pink (known) / red+underline (unknown) / light grey (args) / orange+underline (ambiguous); ghost hint for missing required args; token ghost consumed char-by-char with `→`; v1 argument completion = the `wait` choice provider, more providers added to the `ISlashCommandArgumentFormatProvider` set — [prototype](../prototypes/09-highlighting-and-argument-completion.md).
- [Autocomplete popup](issues/08-autocomplete-popup.md) (prototype): `Popup` anchored via `HighlightTextPresenter.GetCaretRectIn` (TextBox keeps focus); states closed/open/filtering/empty/unknown/argument; `Enter` accepts when open else sends, `Tab` accepts, `Esc` closes; accept inserts the fully-qualified token + space; list sorted by `Order` then name with defeated marks — [prototype](../prototypes/08-autocomplete-popup.md).
- [Lua API exposure](issues/10-lua-api-exposure.md): `dass.commands.list()` + `await dass.commands.invoke(token, arguments?, generate?, wait_for_generation?)` (snake_case params; arguments = raw string or a Lua table serialised back to argument text); routed through the insertion service, message authored by login `"Script"`; returns `{ success, error, message_index, fingerprint }`.
- [Disabled-message toggle](issues/07-disabled-message-toggle.md): mutable `ChatMessage.IsDisabledForAgents` (all message types, **including its own sender agent**), enforced first in `MessageVisibilityService.CheckVisibility`; `MessageVisibility.OnlyUsers` left untouched; SCM unaffected (checkpoints still contribute); UI = eye toggle at the message bottom + dim to ~0.7–0.8 opacity.
- [Command fingerprint](issues/06-command-fingerprint.md): `SlashCommandFingerprint : AdditionalChatData`, data-only + persisted (`IsTemporary = false`), a flat BSON-safe snapshot (token, command, namespaces, source, raw text, raw positionals/keyed, `ModelFacingMode`, intent/outcome, `Status { Executed, Failed, Cancelled }`, `Error`, `EffectSummary`); created/owned by the insertion host; never rendered into the prompt.
- [Chat-level sub-agent tool policies](issues/13-chat-level-sub-agent-tool-policies.md): `ChatSubAgentSettings` gains a `ToolPolicyMask Policy` with `[InheritedChatSetting]`; `agent-callsub` and the `/agent:*` command read it instead of the caller agent's policy; UI mirrors the BEHAVIOUR POLICY section of the agent tool settings.
- [Skill- and agent-command semantics](issues/04-skill-and-agent-command-semantics.md): `/skill:*` injects the body via a new reusable `AdditionalMessageContentPart` (appended to `Content` in `ChatMessageQuoteRenderer`, chip badge), args substitute into the body, `AdditionalMessagePart.IsRestorable` added; `/agent:*` launches a sub-agent like `agent-callsub` (fresh `AgentTaskLaunchParameters` + resolver), attaches to `message.AgentTasks`, fires-and-forgets with a `wait` named arg; policies come from `ChatSubAgentSettings` (ticket 13).
- [Message-insertion and execution service](issues/05-message-insertion-and-execution-service.md): new chat-scoped `IChatMessageInsertionService` (ChatOperationService delegates to it); `IChatExecutionTokenService` with `ChatExecutionLevel { None, Operation, Command, AgentSequence, Agent, Message }`, `WithToken(level, out ct)`, `TryCancel(level)`, `ExecutionCancellationToken` = the CTS itself; flow = resolve/validate (block on failure) → insert → execute → `GenerateIntent && outcome` → generate; `ChatMessage.Error` moved up from `AssistantMessage`; `TryInsertUserInputAsync` returns the inserted `ChatMessage`.
- [Command resolution and namespacing](issues/02-command-resolution-and-namespacing.md): `:`-separated token (last segment = name, earlier = namespace qualifiers matching type/pack); types `skill`/`agent` (+ reserved `tool`/`script`); bare name resolves by `OverrideOrder`→`Order` with the losers namespace-only and marked in the UI; fully-qualified dedup key (so `AddonSetCollectorBase.GroupBy(Name)` becomes `GroupBy(GetDeduplicationKey)`); chat-scoped `ISlashCommandResolver` → `Unknown`/`Ambiguous`.
- [Command formats and argument schema](issues/12-command-format-and-argument-schema.md): new `SlashCommandArgumentSchema` (ordered positionals + keyed map + rest-positional) + `ISlashCommandArgumentFormatProvider` (validate/convert/complete); `/ns:cmd pos… key=value` grammar with quote-stripping; file/scriptable formats designed, not implemented; command names are completed by the general autocomplete service.
- [Command registry and sources](issues/01-command-registry-and-sources.md): `SlashCommandInfo` (kind `Command` → `SlashCommand`) + `ISlashCommandProvider` + `SlashCommandSetCollector`; chat-level, all commands on by default; `ICommandExecutor` interface that mutates the target message; argument schema deferred to ticket 12.

## Not yet specified

- Scriptable-command **script-side API** (what a command script may call: message mutation / tool calls / agent calls) — the engine *contract* is designed (ticket 12); the API surface is not.
- Structured command **results** in the fingerprint (beyond `EffectSummary`).
- **Tools-v2 argument schema** (richer than JSON-schema; can mark a "main" argument) and the tool-command format built on it.
- Command **composition / pipelines** (several commands in one message).
- Interactive/awaiting commands (pause for tool consent or Lua UI) — the rejected `AwaitUser` directive; revisit when tools/scripts land.
- **Blazor WebUI** frontend.
- External **RPC** frontend (not implemented yet).
- Command permissions/policy (allow/deny per chat or agent) and localization specifics.
- Activating `ModelFacingMode` values `Neutral` / `Hidden`.

> Note for tickets 08/09: **completing command names and namespaces is the general autocomplete service's job**, not a command's argument schema (see ticket 12); the autocomplete must also **mark defeated (shadowed) commands** and offer their qualified variants (ticket 02).

## Out of scope

- **`ToolExecutionService` decomposition** — splitting the monolith into stages and generalising the message target beyond `AssistantMessage`. Deliberately deferred by the user ("not this time"); a separate future effort.
- **Implementation / build** — this map ends at the spec; coding is a separate effort.
- A broader **chat-control Lua API** (`dass.chat.*` and similar) — adjacent to commands, but a separate effort.
