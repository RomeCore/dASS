# Participant permissions in the WebUI

Status: open
Type: task
Blocked by:

## Requested

- Participants of a shared chat differ in authority: some may control the chat flow from the WebUI, the others must not.
- Flow control is understood as restarting generation, managing branching, and running commands.
- The target shape is a Discord-like room: many participants, roles, moderation, shared presence.
- The web surface is considered unfinished, and its intended shape is not settled; the permission model is part of defining what it is.
- The model itself is open. The request is the idea, not a design.

## Proposed design

- **A permission has no subject yet, so membership comes first.** Accounts are application-level and self-registered, the chat roster holds only local users, and a connected web participant belongs to no roster at all. The roster has to become the access list of the room: membership decides who can open it, and authority is a property of a membership.
- **Account, participant and addressing key are three things fused in one type.** Credentials belong to the account and follow a person across chats; persona (display name, description, image) belongs to the participant and differs per chat; the login stays the join key and the key used for message audience.
- **Store capabilities, present roles.** A flag set per membership, with roles as named presets, one default role for a new member, and an explicit owner. A role enumeration would be asked for a fifth role within a week.
- **Chat-flow control and host-affecting execution are separate classes.** Operator must not imply tool execution: deleting a message is a social act, running a tool edits the machine. The execution grant stays owner-only and is never bundled into a preset.
- **Destructive capabilities carry a scope.** Acting on one's own message and on someone else's are different rights, and a binary operator-versus-nobody model forces the second one on anyone who needs only the first.
- **Grants are made on the desktop, only there, and only by the host.** A web participant can never extend its own authority; the desktop is the host and is never checked.
- **Enforcement sits at the operation seams, never in the components.** Three seams carry it: the shared chat-operation surface, the message insertion path where text becomes command execution, and cancellation. Components read the same answer to hide what is denied.
- **The invoking participant is resolved from the ambient identity of the call.** Not from a component parameter and not captured in a constructor: chat-scoped services are lifted into the web host as singletons, so a captured participant would be whoever arrived first.
- **The command gate ships before the model.** A command can be run today from the plain input by naming it, without policy and without confirmation, while the room is open to any registered account. That is a hole, not a missing feature, and it is closed first.
- **Refused: per-channel permission overrides.** Per-chat roles already express the same thing, and the override triad is the most confusing part of the shape being imitated.
- **Refused: a per-participant branch pointer.** The branch pointer stays shared chat state and switching stays a gated action. A per-viewer history is a new state axis rather than a permission, and a different and much larger ticket.
- **Refused: generation starts because an operator said so.** Readiness remains a consensus of the participants; the privileged capability is overriding it - starting a turn, rewinding, aborting - not replacing it.
- **Open registration belongs to this ticket, not next to it.** An account is created without the access password and written into application settings, from the same host that then serves the chat. Invite-based joining fits the target shape: the invite carries the role, and the roster decides access.
- **Defaults are chosen per room type, not per checkbox.** A social room and an operator console want opposite defaults; without presets every room keeps the widest one.
- **Who did it is part of the model.** Once the flow can be changed, who restarted a turn or switched a branch is asked daily; the existing per-message metadata is enough to record it.
- **Presence already exists, and typing is not a permission.** The online set is tracked already; typing would be ephemeral per-participant state.

## Prerequisite

- A participant identity that resolves reliably on every request of the interactive session. The current resolution path depends on the request context, which is not guaranteed to survive into the interactive lifetime; every check here stands on that seam.

## Not in scope

- Several rooms served by one web host. Whether the web side is one room or a server of rooms has to be decided first, because it decides whether authority lives at room level or above it. This ticket stays at room level.
- Private per-participant state and per-participant prompt projection, which are a separate axis.
- Slicing the model into implementable tickets.
- Moderation beyond removing a member from the roster.
- Cosmetics: role colours, mentions, attachment rendering.

## Comments

- The room metaphor and the generation metaphor answer different questions. Membership, visibility and moderation are social and follow role-shaped habits; generation is a single exclusive resource shared by the room, and its natural shape is a lobby with a ready-check, which the existing readiness handshake already is.
- Both answers to the hosting question are workable, as long as the level of authority follows the level of the room.
