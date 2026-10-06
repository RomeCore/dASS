# 06: Chat-level sub-agent tool policy

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Move the tool behaviour policy for sub-agent calls to the chat level, so `/agent:<name>` (Stage 3) and `agent-callsub`
use one policy configured once per chat instead of inheriting the calling agent's tool policy.

## Acceptance criteria

- [ ] `ChatSubAgentSettings` gains `ToolPolicyMask Policy` marked `[InheritedChatSetting]` — **no** explicit default level
      (the chain is Chat(profile) → Application).
- [ ] `agent-callsub` (`AgentSubAgentTools`) reads the chat policy instead of the calling agent's policy
      (via `IChatSettingsService` or an equivalent accessor) and feeds it into the resolved sub-agent launch parameters.
- [ ] The chat sub-agent settings UI renders a BEHAVIOUR POLICY section mirroring `AgentToolSettingsViewModel`
      (`ToolBehaviourMaskItem`, `GetPolicyMaskState`/`SetPolicyMaskFlag`).
- [ ] `ISetPolicyMaskFlag` is moved out of `AgentToolSettingsViewModel.cs` into a shared location both the agent and chat
      settings VMs implement.
- [ ] Generated effective getters/setters are used (no hard-coded effective policy).
- [ ] The solution builds; the chat policy round-trips and visibly affects `agent-callsub`.
