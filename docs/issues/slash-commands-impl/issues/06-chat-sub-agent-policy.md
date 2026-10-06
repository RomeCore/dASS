# 06: Chat-level sub-agent tool policy

Status: resolved
Type: task
Blocked by:

## What to build

Move the tool behaviour policy for sub-agent calls to the chat level, so `/agent:<name>` (Stage 3) and `agent-callsub`
use one policy configured once per chat instead of inheriting the calling agent's tool policy.

## Acceptance criteria

- [x] `ChatSubAgentSettings` gains `ToolPolicyMask Policy` marked `[InheritedChatSetting]` — **no** explicit default level
      (the chain is Chat(profile) → Application).
- [x] `agent-callsub` (`AgentSubAgentTools`) reads the chat policy instead of the calling agent's policy
      (via `IChatSettingsService`) and feeds it into the resolved sub-agent launch parameters.
- [x] The chat sub-agent settings UI renders a BEHAVIOUR POLICY section mirroring `AgentToolSettingsViewModel`
      (`ToolBehaviourMaskItem`, the shared `ToolPolicyMaskEditing` state helpers).
- [x] `ISetPolicyMaskFlag` is moved out of `AgentToolSettingsViewModel.cs` into a shared location both the agent and chat
      settings VMs implement.
- [x] Generated effective getters/setters are used (no hard-coded effective policy).
- [x] The solution builds; the chat policy round-trips and visibly affects `agent-callsub`.

## Answer

- `ChatSubAgentSettings.Policy : ToolPolicyMask` with `[InheritedChatSetting]` (no explicit level); the generated
  `PolicyInheritance` / `GetEffectivePolicy()` / `SetEffectivePolicy(...)` are used.
- `agent-callsub` (`AgentSubAgentTools.GetChatSubAgentPolicy`) resolves `IChatSettingsService` from
  `TriggeredChat.Services` and reads `Settings.SubAgents.GetEffectivePolicy()`, passing it to
  `ISubAgentTaskParamsResolver.Resolve(..., policyOverride)`; the resolver applies the override instead of the
  caller's `AutoApproveBehaviours` / `DisallowedBehaviours`.
- The chat sub-agent settings view renders a BEHAVIOUR POLICY section (inheritance combo `AllProfile` + category
  toggles), identical in shape to the agent tool settings view; a new `settings.sub_agents.behaviour_policy` locale
  key was added (iv + ru-RU).
- `ISetPolicyMaskFlag` moved to `LLM/MVVM/Settings/ISetPolicyMaskFlag.cs`; the policy bit-twiddling was extracted into
  `ToolPolicyMaskEditing` (`GetFlagState` / `SetFlag`), now shared by both view models.

No new tests (settings/UI change). Full suite: 801 passed, 1 skipped; main + desktop builds green.
