# 18: Disabled-for-agents message flag

Status: resolved
Type: task
Blocked by: 17

## What to build

A universal, mutable flag on the message that hides it from **every** agent while keeping it in the transcript and
the UI: the model side of the disabled-message toggle ([design ticket 07](../slash-commands/issues/07-disabled-message-toggle.md)).

- `bool ChatMessage.IsDisabledForAgents { get; set; } = false` — all message types, part of the domain, persisted.
- Enforced **first** in `MessageVisibilityService.CheckVisibility`, before the type switch, so the message is invisible
  to every agent — including its own sender (the assistant same-agent shortcut is bypassed).
- Persisted by the message synchronizer for every role.
- `MessageVisibility.OnlyUsers` and the other facets are left untouched.

The flag is **inert** on its own: nothing sets it yet, and hiding a message must not orphan SCM data — that is ticket
19. The toggle that sets the flag is ticket 20.

## Acceptance criteria

- [x] `ChatMessage.IsDisabledForAgents` exists (default `false`), is `Locale`-free and part of the domain.
- [x] `MessageModel.IsDisabledForAgents` exists and round-trips through `MessageDatabaseSynchronizer` for **both**
      user and assistant roles.
- [x] `MessageVisibilityService.CheckVisibility` returns an invisible result for a disabled message, before the type
      branches.
- [x] Tests: the disabled rule for a user message and for an assistant message owned by the agent itself.
- [x] The solution builds; the (filtered) test suite stays green.

## Answer

- **`ChatMessage.IsDisabledForAgents`** (mutable, `SetProperty`, default `false`) sits on the base message.
  `MessageModel` gains the matching field; `MessageDatabaseSynchronizer` copies it in the common part of `CopyToModel`
  (so it persists for both roles) and sets it on both `UserMessage` and `AssistantMessage` in `CreateFromModel`.
- **Enforcement.** `MessageVisibilityService.CheckVisibility` opens with an unconditional check
  (`message?.Message?.IsDisabledForAgents == true` → `MessageVisibilityResult(false, _, MessagePartsFacet.None, …)`),
  before the `UserMessage` / `AssistantMessage` branches, so no branch — not even the assistant same-agent shortcut —
  can resurrect a disabled message.
- **Tests.** `MessageVisibilityServiceTests` covers the user case and the own-sender assistant case (the disabled path
  returns before any dependency is touched, so the service is constructed with `null!` collaborators).

Builds: main + desktop green. Filtered runs: prompting 81, storage 27.

## Comments

- Ticket 19 lands the SCM decoupling that makes the flag safe for messages carrying anchors/deltas/stamps; ticket 20
  adds the UI that can actually set the flag.
