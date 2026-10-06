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
- [ ] [02 — `AddonSetCollectorBase.GetDeduplicationKey`](./issues/02-addon-collector-dedup-key.md)
- [ ] [03 — `AdditionalMessagePart.IsRestorable`](./issues/03-additional-part-restorable.md)
- [ ] [04 — Move `Error` up to `ChatMessage`](./issues/04-message-error-base.md)
- [ ] [05 — Multi-level `IChatExecutionTokenService` (remove `Chat.GenerationCts`)](./issues/05-execution-token-service.md)
- [ ] [06 — Chat-level sub-agent tool policy](./issues/06-chat-sub-agent-policy.md)

## Stage 1 — Command engine core

- [ ] 1.1 `SlashCommandInfo` (+`SlashCommandChange`), `ModelFacingMode`, `ICommandExecutor`, execution context/result.
- [ ] 1.2 `ISlashCommandProvider` + providers: skills, sub-agents (derived `SlashCommandInfo`).
- [ ] 1.3 `SlashCommandSetCollector` + `SlashCommandSet` + chat-level `EnableCommands` + DI.
- [ ] 1.4 `ISlashCommandResolver`: token grammar, namespaces (type + pack), aliases, conflict resolution, `Unknown`/`Ambiguous`.
- [ ] 1.5 Argument grammar: schema, tokenizer, keyed/positional/rest parsing, validation.

## Stage 2 — Message-insertion / execution host

- [ ] 2.1 `IChatMessageInsertionService` + `UserInputInsertionResult`; `ChatOperationService` delegates.
- [ ] 2.2 Flow: resolve → validate (block) → insert → execute → generate; runtime errors on the message; edits don't re-run commands.
- [ ] 2.3 `SlashCommandFingerprint` + `SlashCommandExecutionStatus`.

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
