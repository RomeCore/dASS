# 03: Add `AdditionalMessagePart.IsRestorable`

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Give message parts a flag that decides whether editing a message restores the part into the draft input state.
Default `true` preserves today's behaviour; command-injected parts (Stage 3) will set it `false` so an injected
skill body is not cloned back into `UserInputState` when the user edits the message.

## Acceptance criteria

- [ ] `AdditionalMessagePart.IsRestorable { get; set; } = true` exists and is persisted/round-trippable like the other part flags.
- [ ] The edit/restore path skips parts with `IsRestorable == false`.
- [ ] No existing part changes its effective behaviour (default `true`).
- [ ] The solution builds.
