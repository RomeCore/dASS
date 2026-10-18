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

## Cascade reach UI

Ticket 11. The three-way choice *this tool / same kind / every tool* is implemented in the core
(memorization reach and resolution reach), but the panel has no selector for it yet. Duration uses the
existing `Once` / `Session` / `Task` / `Always` options. **Seam:** `ToolApprovalResult` and the
confirmation service already carry the reach; only the panel binding is missing.

## Panel-hosted custom approval content

Ticket 08 / 02. Today a tool's approval provider attaches its own `AdditionalChatData` part (the diff
view) and awaits its own consent source; `ToolConsentPanel` stays the generic shell. A real
`IToolApprovalContent` contract rendered *inside* the panel is deferred. **Seam:** the provider is already
a descriptor capability and receives the context.

## Script-source synchronization

`ScriptToolOrchestrator` skips `IToolSynchronizationService`. Sync keys are not reentrant: a tool holding
`AllFiles` (or a directory) that calls another tool through `dass.tool.call` would wait forever on the
keys its own caller holds. The decision between key-reentrancy tracking (via `SharedContext` / ambient
state) and a narrower skip rule is deferred. **Seam:** the stage is a service the orchestrator chooses to
call; adding the call back is one line.

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

## Restart with an automatic agent turn

Restart mutates the tool call in place and the new result appears in the next model request. A "restart
and continue" action that starts a new turn is deferred. **Seam:** restart is an orchestrator call; a
follow-up turn would be a chat-execution entry point.

## Web front end participation in the execution path

Spec §14 ("not yet specified"). `LLMDesktopAssistant.Blazor` is not wired to the dispatcher. **Seam:**
stage services are Application-scoped and stateless; a web orchestrator would be a sixth source if the
need appears.

## Sync analyzers beyond the filesystem

Ticket 06 / spec §7. This pass migrates every existing `SynchronizationGroup` to `StaticSyncKeys` and adds
one `IToolSynchronizationAnalyzer` for filesystem paths (the proof of the hierarchical model). Other tools
that could emit argument-derived keys will get analyzers later. **Seam:** `IToolSyncAnalyzer` is already a
descriptor capability; adding one is a local change.

## End-to-end tests

Unit tests cover the pure pieces (binder, accessor, synchronizer, policy, confirmation, cascade,
projections). No fake-LLM chat e2e. **Seam:** the dispatcher is the single entry point an e2e would call.

