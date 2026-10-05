# Disabled-message toggle

Status: resolved
Type: grilling
Blocked by:

## Question

Design the universal **"disabled message"** toggle: a flag on `ChatMessage` that hides the message from *every* agent while keeping it in the transcript and UI (e.g. `/help` disables its own message).

Cover:

- where the flag lives on the model;
- how `MessageVisibilityService` / SCM / prompt rendering consume it (check against the existing visibility facets and `MessageVisibilityFacet`);
- interaction with branches and checkpoints;
- persistence;
- the UI affordance to see / toggle the disabled state.

## Answer

### Flag & enforcement

- New mutable `bool ChatMessage.IsDisabledForAgents { get; set; } = false`, defined on **`ChatMessage`** (all message types), part of the domain, persisted and toggleable after creation.
- Enforced **first** in `MessageVisibilityService.CheckVisibility` — before the type switch — so a disabled message is invisible to **every** agent.
- This includes the message's **own sender agent**: a disabled `AssistantMessage` is not visible even to the agent that produced it. The same-agent shortcut in the `AssistantMessage` branch (`messageAgentId == agent.Id` → always visible) is therefore **bypassed** when the flag is set — which the "first check" placement already guarantees.

### Relationship to `MessageVisibility.OnlyUsers`

- **Kept as-is.** `OnlyUsers` (and `RevealAfterSend`, the `VisibleTo` lists) stay where they are: a `UserMessage`-specific visibility mechanism. `IsDisabledForAgents` is a separate, **universal** (any message type), **mutable** toggle. For a disabled user message both may apply; the flag is simply checked first.

### SCM

- It does **not** break SCM. The message is hidden from the prompt, but a disabled message that carries an anchor / delta / checkpoint **still contributes it** — checkpoints are processed before the visibility check in `AgentEffectiveMessagesProvider`. Enforcement lives **only** in `CheckVisibility`; no special handling elsewhere.

### Persistence & branches

- A domain property, persisted by the message synchronizer; the toggle can be flipped after the message was created.

### UI

- A **toggle button at the bottom of the message**, next to the "render markdown" toggle; icon = **Eye**.
- When the message is hidden from agents, the whole message is **dimmed** (opacity ≈ 0.7–0.8).

### v1 usage

- The toggle and the flag exist in v1. No v1 command uses it (native commands are fog); a command's executor can set it later (e.g. `/help` disabling its own message).

## Comments

- Single round. The `OnlyUsers` overlap was raised and explicitly left untouched.
- Follow-up: the flag hides the message **even from its own sender agent** (the same-agent shortcut is bypassed).
