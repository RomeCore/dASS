# P6a — DefaultApprovalLevel is not applied to tools without a change entry

Status: resolved
Type: task
Blocked by:

## Question

`ToolsetConfiguration.DefaultApprovalLevel` is shown by the tool cards
(`AddonCardToolPolicyMaskBlock.EffectiveApprovalLevel`,
`ToolAddonCardFactory`, `AddonCardToolSpecifiersBlock`) as
`change.ApprovalLevel ?? addon.ApprovalLevel ?? toolset.DefaultApprovalLevel ?? PolicyBased`,
but the runtime applies it only inside `ToolsetCollector.ApplyChange`, which
`AddonSetCollectorBase.GetAddonsWithChanges` calls **only** when the tool has an
entry in `ToolsetConfiguration.Changes`.

Consequence: with a toolset default of `AlwaysAsk` or `AlwaysDisallow`, a tool
the user never touched keeps `ApprovalLevel == null`, which resolves to
`PolicyBased` through `IToolDescriptor.ApprovalLevel`. The card says one thing,
the pipeline does another — a security-relevant mismatch.

The same pass should stop relying on the collector to stamp `ContextFactory`
only on changed tools (the chat entry point has a fallback, but the stamping
must be uniform).

## Decisions

- A new virtual hook `AddonSetCollectorBase.ApplyDefaults(target, setConfig, agent)`
  is called for **every** included clone in both branches of
  `GetAddonsWithChanges`, after `ApplyChange` when a change exists and before
  `Freeze()`. The default implementation is a no-op.
- `ToolsetCollector` overrides the hook: it stamps `ContextFactory` and resolves
  `ApprovalLevel = change?.ApprovalLevel ?? tool.ApprovalLevel ?? default ?? PolicyBased`.
  `ApplyChange` keeps only the per-tool overrides.
- `AvailableTools` (the agent-less list) keeps no default: the default is a
  toolset/agent concept.
- The resolution is a single inline expression in `ApplyDefaults`; a
  collector-level test proves the hook runs for a tool without a change entry
  and covers the precedence matrix through the real wiring.

## Answer

Landed on 2026-10-18 on `tool-execution-v2`.

- `AddonSetCollectorBase` gained `ApplyDefaults(target, setConfig, agent)`. It is called for **every**
  included clone in both branches of `GetAddonsWithChanges` (after `ApplyChange` when a change exists)
  and before `Freeze()`. The default implementation is a no-op.
- `ToolsetCollector` overrides it: it stamps `ContextFactory` on every tool and resolves
  `ApprovalLevel = target.ApprovalLevel ?? toolset.DefaultApprovalLevel ?? PolicyBased`.
  `ApplyChange` now only folds the per-tool change into `target.ApprovalLevel`; it no longer resolves
  the toolset default itself.
- `AvailableTools` (the agent-less list) stays default-free; the default is applied only when an agent
  toolset is resolved.
- The resolution stayed one inline expression. A pure static helper was considered and dropped: the
  collector-level test covers the real wiring instead.
- Tests: `ToolsetCollectorDefaultsTests` covers a tool without a change entry (gets the default and a
  `ContextFactory`), a tool-declared level winning over the default, and a per-tool change winning over
  the default. The fixture fakes `IChatSettingsService` (`ChatSettingsService` loads the user's settings
  file and fails on a corrupt `$type`), `IAgentManagementService` and `IMCPManagementService`.
- Core suite: 1247 tests, 1245 passed, 1 skipped (known YamlDotNet colon skip). The only failure
  observed was an unrelated LiteDB flake in `SlashCommandFingerprintTests`.
