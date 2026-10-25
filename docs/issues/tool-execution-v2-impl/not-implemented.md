# Not implemented in this effort

Deferred on purpose. Each entry states the seam that keeps it reachable later. When an item lands, delete
it from here and update the owning spec/ticket.

## Approval helper

Spec §12. Modes (`manual` / `assistant` / `worker`), `LeadDelay` / `SettleDelay` / `ExtraPrompt`,
configurable model, context assembly, the `{ verdict, notes_for_user, notes_for_agent, requested_data,
override_agent_intent, new_arguments }` output. **Seam:** `ApprovalHelperConfig` is already carried by
`IToolDescriptor`, and `ToolConsentPanel` is the shell it will live in. The tool system is built and works
without it.

## Lua hooks

Spec §14 / ticket 12. Positional `BeforeX` / `AfterX` points, verdict shape, ordering, failure handling,
sequential-vs-parallel. **Seam:** each stage is a separate service, so a hook call belongs to the stage
that owns the point; `ArgumentsOverride` already exists as the ephemeral channel with validation at
install.

## Cascade approval (P7) — not started at all

The whole of ticket 11 is open: neither the reach model (this tool / same name / every tool) nor the
cascade to the sibling awaiting calls of a batch exists. `ToolApprovalResult` carries only
`Memorization` (the duration), `ToolConfirmationService` decides one call and never looks at
`ctx.Host.ToolCalls`, and `ToolConsentPanel` has no reach selector. **Seam:** the reach is a field on
`ToolApprovalResult` and the siblings are reachable through `IToolExecutionContext.Host`
(`ChatMessage.ToolCalls` / the task's awaiting calls), so the cascade belongs to the confirmation
stage and the picker to the panel.

## Panel-hosted custom approval content

Ticket 08 / 02. Today a tool's approval provider attaches its own `AdditionalChatData` part (the diff
view) and awaits its own consent source; `ToolConsentPanel` stays the generic shell. A real
`IToolApprovalContent` contract rendered *inside* the panel is deferred. **Seam:** the provider is already
a descriptor capability and receives the context.

## Script-source synchronization

`ScriptToolOrchestrator` skips `IToolSynchronizationService`, and that skip is now **load-bearing**: a
scriptable tool may hold `AllFiles` (from its `sync` declaration or its `behaviours`) and call another tool
through `dass.tool.call`, so synchronizing the nested call would deadlock it against its own caller. Keys
are not reentrant. The remaining decision is key-reentrancy tracking (via `SharedContext` / ambient state)
versus keeping the skip; until then a nested call is unsynchronized by construction. **Seam:** the stage is
a service the orchestrator chooses to call; adding the call back is one line.


## Approval-time argument editing

`ToolApprovalResult.EditedArguments` / `OverrideModelIntent` / `ApprovalData` exist and the confirmation
stage installs them (`call.Arguments` or `ctx.ArgumentsOverride`, plus `ctx.ApprovalData`), but no surface
produces them yet: the generic panel has no editor. **Seam:** extend `ToolConsentPanel`; a provider
(or the approval helper) can set the fields on the returned `ToolApprovalResult` directly.

## Preview invalidation and re-validation after an argument edit

An approval edit is installed without clearing `ctx.SharedContext` and without re-running
`IToolValidationService`; the preview artifact and the validation result therefore belong to the old
argument set. **Seam:** `ToolConfirmationService.Apply` is the single place the edit is installed; a
re-preview loop and a validation call belong there once the lifecycle is decided.

## Real argument validation

Spec §4 / ticket 07. `IToolValidationService` ships as a stub that always returns valid. The interface,
the two channels and the accessor are real. **Seam:** one service implementation, no call-site changes.

## Restart with edited arguments

A restart re-runs a finished call on the same object, and `call.Arguments` is the persisted channel for
an edit — but no surface produces one: the call card has no argument editor and no approval surface is
involved in a restart. **Seam:** `ChatToolCallService.RestartAsync` is the single place that starts the
run, so an edited `JsonObject` is one parameter away; it must be installed into `call.Arguments` before
the dispatch.

## Restart with an automatic agent turn

Restart mutates the tool call in place and the new result appears in the next model request. A "restart
and continue" action that starts a new turn is deferred. **Seam:** restart is an orchestrator call; a
follow-up turn would be a chat-execution entry point.

## Web front end participation in the execution path

Spec §14 ("not yet specified"). `LLMDesktopAssistant.Blazor` is not wired to the dispatcher. **Seam:**
stage services are Application-scoped and stateless; a web orchestrator would be a sixth source if the
need appears.

## Synchronization edges the queue does not cover

Spec §7. The keyed queue, the read/write access and the skip surface are landed; what remains outside them is
deliberate:

- **`wait=false`.** A shell or Python call that does not wait releases its keys when the tool call returns,
  while the process keeps running. No key can cover a background process; a caller that needs the guarantee
  has to wait.
- **The user's skip.** Cancelling `IToolCall.SynchronizationWait` runs the call without claiming anything.
  The ordering guarantee is lost by design, and the card keeps the mark so the loss is visible.
- **No timeout.** A queued call waits until it is granted, cancelled or skipped; a long shell does not expire.
- **MCP optimism.** MCP tools claim `Read AllFiles`, so they run beside each other and beside local readers
  rather than freezing the disk. A server that writes therefore writes unsynchronized. Flipping the single
  word to `Write` is the whole fix if it ever bites.
- **Scriptable tools.** A script is opaque: its lock is as honest as its `sync` declaration or its
  `behaviours`, and a script that under-declares escapes synchronization entirely. Same class of limit as
  the skip, but silent.

**Seam:** every item is a decision inside one of three places — the descriptor's keys, the analyzer, or the
`CancellationTokenSource` on the call.


## End-to-end tests

Unit tests cover the pure pieces (binder, accessor, synchronizer, policy, confirmation, cascade,
projections). No fake-LLM chat e2e. **Seam:** the dispatcher is the single entry point an e2e would call.

