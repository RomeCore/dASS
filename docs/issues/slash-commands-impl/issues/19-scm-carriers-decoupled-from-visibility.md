# 19: SCM carriers decoupled from message visibility

Status: resolved
Type: task
Blocked by: 18

## What to build

The disabled-message flag (ticket 18) removes a message from `effectiveContext.Messages`, but SCM state lives on
messages and must survive: a message hidden from agents must not silence the anchors, deltas and stamps it carries.
The design ticket 07's claim that "enforcement lives only in `CheckVisibility`" is wrong for SCM — checkpoints are
immune because `AgentEffectiveMessagesProvider` collects their carriers from the raw window, but anchors, deltas and
in-window stamps are not.

Decouple the three at-risk SCM readers from the visibility-filtered effective set, in the style of
`PromptSupersedeContextProcessor` (which already walks the raw history):

- **Anchor lookup** (`PromptAnchoredSectionProcessor.Process`): locate the boundary and the live anchor in the raw
  `Chat.Messages`, not in `effectiveContext.Messages`.
- **Delta rendering** (`AgentPromptComposer`): a delta carried by a hidden message must still be announced.
- **In-window stamp rendering** (`AgentPromptComposer`): likewise.

Boundary semantics are unchanged (any enabled checkpoint). The anchor is reused if it lies after the boundary; a
rebaseline installs the new anchor on the **first raw message** after the boundary (it may be hidden — SCM is now
decoupled).

## Acceptance criteria

- [x] `PromptAnchoredSectionProcessor` finds the boundary and the live anchor by walking the raw history; a hidden
      carrier is no longer missed.
- [x] Rebaseline installs the anchor on the first raw message after the boundary.
- [x] `AgentPromptComposer` surfaces the deltas/stamps of hidden carriers and renders them at the nearest visible
      assistant message at or after the carrier, else the pending turn.
- [x] Nothing changes when no message is hidden (empty map → the loop is byte-identical).
- [x] Tests: a hidden carrier's announcement is hosted by the next visible assistant (and by the pending turn when
      there is none); visible and foreign-agent carriers are not collected; anchor lifecycle tests still pass.
- [x] The solution builds; the (filtered) test suite stays green.

## Answer

- **`PromptAnchoredSectionProcessor`** now takes `IChatSettingsService` (to compute the agent's disabled-checkpoint
  mask) and walks `chat.Messages` for the boundary (newest enabled checkpoint of any kind) and for the live anchor
  (newest agent anchor after the boundary). The delta-collection walk and the rebaseline target also use `chat.Messages`
  (`targetIndex = boundaryIndex + 1`, the first raw message after the boundary). The pending-assistant precondition is
  read from `chat.Messages[^1]`.
- **`AgentPromptComposer`** gained an internal pure helper `CollectHiddenAnnouncements(rawMessages, effectiveContext,
  agentId, anchor, effectiveStartIndex)`: it walks the raw history from the effective window start, skips the messages
  already in the effective set, and maps each hidden agent-owned assistant carrier's delta (matching the live anchor) and
  stamp snapshots to the effective index of the nearest visible assistant message at or after it, else the pending turn.
  The main loop injects those snapshots when it reaches the host message — **only** when the map is non-empty, so the
  visible path is unchanged.
- **Safety property.** With nothing hidden the map is empty and the prompt bytes are identical; the decoupling is
  additive.
- **Tests.** `HiddenScmAnnouncementTests` covers the next-visible-assistant host, the pending fallback, the visible
  carrier (not collected) and the foreign-agent carrier (ignored). `PromptStateStageTests` was updated to attach a real
  checkpoint (the boundary is now found in the raw history) and to construct the processor with a settings fake.

Builds: main + desktop green. Filtered runs: prompting 81.

## Comments

- Supersedes the "SCM is unaffected with no special handling" line of design ticket 07 for anchors/deltas/stamps.
- The pending assistant message can never be hidden (its action row only exists once it is completed), so the pending
  precondition is safe.
