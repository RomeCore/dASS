# Slash commands v1 — implementation plan

Implementation effort for the locked spec [`../slash-commands/spec.md`](../slash-commands/spec.md).
The design is settled (all 13 decision tickets in [`../slash-commands/issues/`](../slash-commands/issues/) are resolved);
this directory is the *implementation* checklist — a task graph of tracer-bullet tickets, numbered from `01`.

> Do not confuse the two directories: `../slash-commands/` is the **design** effort (map + spec + decisions);
> this one is the **implementation** effort (plan + tickets).

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done.

## Stage 0 — Architecture / prefactor

General infrastructure everything else leans on. No user-visible behaviour; the exit criterion is a green build + green tests.

- [x] [01 — Rename `AddonKind.Command` to `SlashCommand`](./issues/01-slash-command-kind-rename.md)
- [x] [02 — `AddonSetCollectorBase.GetDeduplicationKey`](./issues/02-addon-collector-dedup-key.md)
- [x] [03 — `AdditionalMessagePart.IsRestorable`](./issues/03-additional-part-restorable.md)
- [x] [04 — Move `Error` up to `ChatMessage`](./issues/04-message-error-base.md)
- [x] [05 — Multi-level `IChatExecutionTokenService` (remove `Chat.GenerationCts`)](./issues/05-execution-token-service.md)
- [x] [06 — Chat-level sub-agent tool policy](./issues/06-chat-sub-agent-policy.md)

> **Stage 0 complete.** All architecture/prefactor tickets resolved.

## Stage 1 — Command engine core

Sliced into tracer-bullet tickets; each is one atomic commit to `main`.

- [x] [07 — Command argument grammar and schema](./issues/07-command-argument-grammar.md) — argument model, format-provider contract, RCParsing parser (maintainer-owned grammar) and the binder. **First**, because the executor context consumes the parse result.
- [x] [08 — Command model and executor contracts](./issues/08-command-model-and-executor.md) — `SlashCommandInfo`/`SlashCommandChange`, `ModelFacingMode`, `ISlashCommandExecutor`, context/result, the temporary stub, and the inert locator/parser/descriptor.
- [x] [09 — Command providers (skills, sub-agents)](./issues/09-command-providers.md) — `ISlashCommandProvider` + the two derived providers and the order tiers.
- [x] [10 — Command set collector, settings and DI](./issues/10-command-collector-and-settings.md) — `SlashCommandSetCollector`, the fully-qualified dedup key, `ChatCommandSettings` (`EnableCommands`).
- [x] [11 — Command resolver and namespacing](./issues/11-command-resolver.md) — pure matcher, token grammar, `SlashCommandResolution` (status + defeated), chat-scoped resolver.

Blocking edges: `08 ← 07`, `09 ← 08`, `10 ← 09`, `11 ← 10`. Exit criterion: green build + green tests, no user-visible
behaviour yet (the dispatch host is Stage 2).

> **Stage 1 complete.** Tickets 07–11 resolved.

## Stage 2 — Message-insertion / execution host

- [x] [12 — Localizable message error and `LocaleFormattedKey`](./issues/12-localizable-error-and-formatted-key.md) — prefactor: the error of a message becomes a `LocaleKeyBase` and a format-argument key type lands. **First**, because the host and the fingerprint carry typed errors.
- [x] [13 — `SlashCommandExtractor` and `SlashCommandInfo.CanonicalToken`](./issues/13-command-extractor-and-canonical-token.md) — move the `/` marker out of the matcher; give a command its slash-free canonical token.
- [x] [14 — Chat message-insertion service (command host)](./issues/14-chat-message-insertion-service.md) — the resolve → validate → insert → execute → generate host; the guard; the view-model pre-flight.
- [x] [15 — Command fingerprint and execution status](./issues/15-command-fingerprint.md) — the persisted trace of an invocation and `SlashCommandExecutionStatus`.

Blocking edges: `13, 14, 15 ← 12`; `14 ← 13`; `15 ← 14`.

> **Stage 2 complete.** Tickets 12–15 resolved. The send path now runs through `IChatMessageInsertionService`, a command's message carries a persisted fingerprint, and a refused command keeps the user's draft.

## Stage 3 — First two commands

- [x] [21 — Command message-content layers](./issues/21-command-message-content-layers.md) — prefactor: `AdditionalMessageContentPart`, localizable `ChipTitle`, `ModelFacingMode` in the result + fingerprint, and the renderer's model-facing content projection. **First**, because both executors write their message through it.
- [x] [22 — `/skill:<name>` executor](./issues/22-skill-command-executor.md) — body injection, argument/variable substitution, chip.
- [x] [23 — `/agent:<name>` executor](./issues/23-sub-agent-command-executor.md) — sub-agent launch, chat-level policy, `wait`, result injection.

Blocking edges: `22, 23 ← 21`; `22` and `23` are independent.

## Stage 4 — Message level

- [x] [18 — Disabled-for-agents message flag](./issues/18-disabled-for-agents-flag.md) — the model flag, its persistence and the first visibility rule.
- [x] [19 — SCM carriers decoupled from message visibility](./issues/19-scm-carriers-decoupled-from-visibility.md) — anchors/deltas/stamps located in the raw history, so a hidden carrier keeps contributing.
- [x] [20 — Disabled-message toggle UI](./issues/20-disabled-message-toggle-ui.md) — the eye toggle (`Eye`/`EyeOff`) and the dimmed message.

## Stage 5 — Input UX (general input completion)

The autocomplete is a **general** input-completion mechanic, deliberately not command-specific — slash commands are
its first source, chat-agent mentions (`@Code Reviewer`, names may contain spaces) a later one. Supersedes the earlier
`5.1`–`5.4` sketch.

- [x] [24 — Edit-check fix](./issues/24-edit-check-fix.md) — prefactor: the view-model pre-flight must validate a
      command on an edit exactly as on a new message (edits run commands). **First**, independent.
- [x] [25 — General input-completion core](./issues/25-general-input-completion-core.md) — the UI-agnostic
      `IInputCompletionSource` / request / result / state / item types and the chat-scoped source resolver.
- [x] [27 — `SlashCommandInputAnalyzer` + highlight provider + theme brushes](./issues/27-input-analyzer-and-highlight.md)
      — the shared pure input analyzer and the real highlighting for the chat input (palette + ghost renderer + theme
      brushes; the debug page runs on it).
- [ ] [26 — `SlashCommandCompletionSource`](./issues/26-slash-command-completion-source.md) — token prefix matching,
      defeated marking and argument completion delegated to `ISlashCommandArgumentFormatProvider`.
- [ ] [28 — `HighlightTextBox` completion hooks](./issues/28-highlight-textbox-completion-hooks.md) — caret accessor,
      key hook, mid-string ghost, `→` char-accept, reset on pointer caret moves.
- [ ] [29 — Autocomplete popup + `InputCompletionViewModel`](./issues/29-autocomplete-popup.md) — the caret-anchored
      popup, its state machine and the wiring into `UserInputView`.

Blocking edges: `26 ← 25, 27`; `28 ← 25`; `29 ← 25, 26, 28`; `24` and `27` independent.
Execution order: `24 → 25 → 27 → 26 → 28 → 29`.

## Stage 6 — Lua API

- [ ] 6.1 `LuaApiCommands` (`dass.commands.list` / `invoke`).

## Stage 7 — Polish

- [ ] 7.1 Localization keys.
- [ ] 7.2 Help/docs (`docs/help/*`, `GLOSSARY.md`).

## Chat commands settings UI (out of plan)

Added after Stage 2 — the plan originally carried no settings UI for commands.

- [x] [16 — Command source kind](./issues/16-command-source-kind.md) — the `SlashCommandSource` enum, stamped on the command and recorded in the fingerprint.
- [x] [17 — Chat commands settings tab](./issues/17-commands-settings-tab.md) — the chat-level command page (gate, set inheritance, editable list), the ADDONS reorder and the "Lua scripts" rename.

## Committed decisions (during implementation)

- **Commits**: straight to `main`, atomic per checklist item.
- **Tests**: TDD for pure logic (resolver, tokenizer/schema, fingerprint, visibility, dedup key); UI verified manually.
- **Migrations**: none — 0 users, no release.
- **`ChatExecutionLevel.Operation`**: taken by `ChatOperationService`; `ChatExecutionService` is reworked separately by the maintainer.
- **`Chat.GenerationCts`**: removed; `IChatExecutionTokenService.ExecutionCancellationToken` is the single source for the UI.
- **`ICommandExecutor` → `ISlashCommandExecutor`**: the implementation name; the locked design docs (`../slash-commands/`) keep the old spelling and are not rewritten.
- **No `Ambiguous`**: the collector collapses same-key duplicates into `Overrides`, and the matcher orders by `OverrideOrder` desc → `Order` asc → fully-qualified key asc, so a true tie cannot occur. `SlashCommandResolutionStatus` is `{ Unknown, Exact, WonOthers }`; `WonOthers` is the UI state formerly called “ambiguous” (orange + underline).
- **`SlashCommandResolution`** gains `Status` and `IReadOnlyList<SlashCommandInfo> Defeated` (the winner + its `Overrides`); `Error` is populated only for `Unknown`.
- **Dedup key**: `string.Join(':', Namespaces.OrderBy(StringComparer.Ordinal)) + ":" + Name` — e.g. `matt-pocock:skill:grilling`.
- **Arguments first**: `07` precedes `08`, because the executor context carries the parse result and the executor declares its schema.
- **Inert addon plumbing for commands** (deviation from ticket 01's “no file locator/parser in v1”): `SlashCommandAddonTypeDescriptor` (`commands`, `UseDefaultSearchService = false`), an empty `SlashCommandFileLocator` and a stub `SlashCommandParser` exist so `AddonSetCollectorBase` can construct. A temporary `StubCommandExecutor` stands in until Stage 3.
- **RCParsing grammar** of the argument parser is maintainer-owned: the agent ships the scaffold + tests, the maintainer writes the grammar.
- **Immutable command schema**: `SlashCommandArgumentSchema` and `SlashCommandArgument` are `init`-only — the schema is semantically atomic and travels with the command definition, and `Immutable*` is only shallowly immutable so the leaf type is `init`-only too. Only the addon's *reference* (`SlashCommandInfo.ArgumentSchema`) is settable, because the addon model is mutable and `Clone()` copies settable properties.
- **`SlashCommandExecutionContext` carries `SlashCommandBoundArguments`** (property `Arguments`) instead of the flat argument fields; `RawArguments` stays on the context (pre-parse), and `RestPositionalArguments` rides inside the bound object.
- **Executor errors are `LocaleKeyBase?`** (`SlashCommandExecutionResult.Error`): a user-facing error stays a locale key until the boundary (message `Error`, fingerprint).
- **`Token` is canonical, `RawToken` is as-typed**; `SlashCommandArgumentsResult` was renamed to `SlashCommandParsedArguments`.
- **`SlashCommandFileLocator` declares `Folders = ["commands"]`** with empty `Extensions` (inert but real); no tests are added until the engine has behaviour to test.
- **One generic `DerivedSlashCommandProvider<TSource>`** instead of two copies: `TypeNamespace` + `CreateArgumentSchema(source)` + `CreateCommandExecutor(source)` (abstract) and a `PopulateFromSource` hook; the base assigns the executor.
- **Availability filtering is centralised in `AddonBase.IsValid`** (virtual, computed, `[*Ignore]`), used by `AddonSetCollectorBase`. Providers read `GetAvailableAddons().Where(a => a.IsValid)` — the *source* collectors; the *command* collector's `GetAddonsForChat()` is what autocomplete/resolution call.
- **Derived commands carry provenance**: `SourcePack` / `Path` / `AddonSource` and `SlashCommandInfo.Source` (the source addon, typed `object?`).
- **`AddonChangedBase.Key` replaces `GetDeduplicationKey`** (the earlier per-collector override): one `virtual string Key => Name` used for dedup **and** change resolution in `AddonSetCollectorBase` and `AddonCardContext`. `SlashCommandInfo.Key` is the fully-qualified identity, so `skill:grilling` and `agent:grilling` never collide. Ticket 02's `GetDeduplicationKey` is removed.
- **`EnableCommands` is chat-local (not inherited)**; `CommandsSet` is `[InheritedChatSetting]`. Commands are chat-level and agent-agnostic: `GetAddonsForChat()` is the only exposed path, `GetAddonsForAgent()` is left unimplemented on purpose.
- **Tokens are slash-free** and the `//` prefix escapes a message from being a command (one slash stripped). `/` knowledge lives only in `SlashCommandMatcher.TryExtractToken` / `UnescapeLeadingSlash`; the resolver and everything downstream is slash-free. `Match` is pure; the resolver folds `winner.Overrides` into `Defeated`.
- **`ChatMessage.Error` is `LocaleKeyBase?`** (was `string?`): runtime errors are stored as keys and localized where displayed. A new `LocaleFormattedKey` (immutable `FormatArgs`, caches its own value, not instance-cached) + `Locale.GetFormattedKey`; both BSON (`LocaleKeyBsonSerializer`) and JSON (`JsonLocaleKeyConverter`) gained a `formatted` discriminator. The message synchronizer now persists `Error` for **all** message roles, not only assistant messages.
- **Stage 2 host split**: `IChatOperationService` keeps the send path and the `Operation` token and delegates to `IChatMessageInsertionService.InsertUserInputAsync(input, generateIntent, editIndex, ct)`; a separate pure `CanInsertUserInput(...)` returns `UserInputInsertionCheckResult` and is called by the view model before it clears the draft. `IChatMessageInsertionService` owns the `Command` token. `EnableCommands == false` disables command handling entirely.
- **`SlashCommandMatcher` knows nothing about slashes**: the marker handling moves to a `SlashCommandExtractor`, and the canonical slash-free token is `SlashCommandInfo.CanonicalToken` (`[JsonIgnore][BsonIgnore][YamlIgnore]`, stored namespace order + name). `RawToken` is the as-typed slash-free token.
- **`SlashCommandSource` is descriptive; the tiers stay the ordering authority**: a derived provider stamps `SourceKind` (ticket 16) and `OverrideOrder` is derived from it through `SlashCommandOrderTiers.ForSource` (`Native → Native`, `Script → Scriptable`, else `Derived`).
- **`SlashCommandFingerprint.Source` (type-name string) is replaced by the `SourceKind` enum** — ticket 15's shape superseded; 0 users, no migrations.
- **The chat commands page is editable at chat level** (unlike the read-only chat skills/sub-agents pages), because commands have no per-agent page: the cards edit `GetEffectiveCommandsSet()` and the page carries a `CommandsSetInheritance` selector. Commands are user-only, so the list is built with a `null` search service (substring fallback) and the BM25 `IAddonSearchService` stays off.
- **The ADDONS section order is tools → skills → sub-agents → memory → commands → prompt contexts → Lua scripts**, and the scripts tab is renamed "Lua scripts".
- **`IsDisabledForAgents` is universal and first-checked**: a disabled message is invisible to every agent (including its own sender), persisted for all roles, and set only by the model/view (never inherited from a setting).
- **SCM carriers are decoupled from visibility**: the anchor boundary/live-anchor/delta walks in `PromptAnchoredSectionProcessor` run over the raw `Chat.Messages` (boundary = the newest enabled checkpoint of any kind; rebaseline installs on the first raw message after it), and `AgentPromptComposer` renders the deltas/stamps of hidden carriers at the nearest visible assistant message at or after them (else the pending turn). `CheckVisibility` stays the single "hidden from agents" gate; only the SCM payload is exempt.
- **The disabled-message toggle is desktop-only** (Blazor WebUI is a v1 non-goal); the flag and the visibility rule are core.
- **Stage 3 is sliced into `21`–`23`** (prefactor → `/skill` → `/agent`). `SkillCommandExecutor` / `SubAgentCommandExecutor` live next to their providers (in `Providers/`), constructed by them with chat-scoped dependencies injected through the provider's constructor.
- **The model-facing content projection lives in `ChatMessageQuoteRenderer`, not `MessageVisibilityService`**: `Raw` → the message content, `Neutral` → `/` + the fingerprint's `RawToken` (the bare name), `Hidden` → nothing, content parts appended in every mode. `SlashCommandExecutionResult` carries a `ModelFacingMode?` override that the host folds into the fingerprint.
- **`AdditionalMessagePart.ChipTitle` is `LocaleKeyBase?`** (chips localize; 0 users, no migration) and `AdditionalMessageContentPart` is the reusable text part a command writes into its message.
- **Skill-variable expansion lives in a static `SlashCommandVariableExpander`** with `string? GetSkillVariable(string name, SlashCommandBoundArguments arguments)`: `ARGUMENTS` = `RawPositionalArguments`, `CLAUDE_SKILL_DIR`/`SKILL_DIR` = `HomeDirectory ?? dirname(Path)`, `SKILL_NAME`, then the process environment; unknown names stay verbatim.
- **`StubCommandExecutor` stays** as the non-null default of `SlashCommandInfo.Executor` until file commands need a real one (its removal would force the property nullable).
- **Autocomplete is a general input-completion mechanic, not a command feature**: a caret-anchored popup (continuations + the current state, which may render with no continuations at all) plus a ghost of the selected continuation. Chat-agent mentions (`@Code Reviewer`, names with spaces) are a later `IInputCompletionSource`; the core (`IInputCompletionSource` / `InputCompletionRequest` / `InputCompletionResult` / `InputCompletionItem` / `InputCompletionState` in `InputCompletion/`) never mentions commands, and the replace `Span` is defined by the source, never by whitespace.
- **Accept replaces the current token**: given `abc|d e f`, accept yields `abc1 2 3`; `→` consumes one real char and commits one ghost char (`abc1| e f`), and only inserts when the real token tail is exhausted (or the caret is at the token end).
- **Mid-string ghost is allowed** — the old "suffix-only" note was wrong: the presenter can insert the ghost at the caret; the caret/selection stay clamped to the real text, and any pointer-driven caret/selection change resets the completion state (popup + ghost) without calling the service.
- **Argument completion is provider-driven**: `SlashCommandCompletionSource` asks the schema slot's `Format.CanComplete` and delegates to `Format.Complete` — no `wait`-specific code, so any existing or new `ISlashCommandArgumentFormatProvider` participates.
- **Edits run commands** — the "edits never run a command" line in the design spec/doc-comments was an error introduced while writing the design docs, not the intent; the pre-flight must validate commands on edits too (ticket 24).
