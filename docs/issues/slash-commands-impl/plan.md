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
- [ ] [13 — `SlashCommandExtractor` and `SlashCommandInfo.CanonicalToken`](./issues/13-command-extractor-and-canonical-token.md) — move the `/` marker out of the matcher; give a command its slash-free canonical token.
- [ ] [14 — Chat message-insertion service (command host)](./issues/14-chat-message-insertion-service.md) — the resolve → validate → insert → execute → generate host; the guard; the view-model pre-flight.
- [ ] [15 — Command fingerprint and execution status](./issues/15-command-fingerprint.md) — the persisted trace of an invocation and `SlashCommandExecutionStatus`.

Blocking edges: `13, 14, 15 ← 12`; `14 ← 13`; `15 ← 14`.

## Stage 3 — First two commands

- [ ] 3.1 `/skill:<name> args…`: executor + `AdditionalMessageContentPart` + `ChatMessageQuoteRenderer` append + chip + arg substitution.
- [ ] 3.2 `/agent:<name> input`: executor (sub-agent launch, attach to `message.AgentTasks`, fire-and-forget, `wait`), policy from `ChatSubAgentSettings`.

## Stage 4 — Message level

- [ ] 4.1 `ChatMessage.IsDisabledForAgents` + first-check in `MessageVisibilityService`.
- [ ] 4.2 UI: eye toggle + dimmed opacity.

## Stage 5 — Input UX

- [ ] 5.1 General autocomplete service (command/namespace matching, defeated marking).
- [ ] 5.2 Autocomplete popup in `HighlightTextBox`.
- [ ] 5.3 Highlight transform provider (palette, ghost text, `→` char-by-char).
- [ ] 5.4 Argument completion provider (`wait=true|false`).

## Stage 6 — Lua API

- [ ] 6.1 `LuaApiCommands` (`dass.commands.list` / `invoke`).

## Stage 7 — Polish

- [ ] 7.1 Localization keys.
- [ ] 7.2 Help/docs (`docs/help/*`, `GLOSSARY.md`).

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
