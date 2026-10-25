# Per-participant private state in a shared multiuser chat

Status: open
Type: task
Blocked by:

## Requested

- Users of the multiuser WebUI own state that only that user can see.
- Scripts and agents must be able to interact with that state.
- The primary use case is a table role-playing session: several players share one chat, each holding material the others must not see.
- The state may take the form of a private per-user interface, structured fields, free variables, or a combination. The shape is not settled and is explicitly left open.

## Proposed design

- **The audience axis already exists; ownership does not.** Read permissions are already expressed per participant, keyed by a user login or an agent id, and already filter per message part. The idea therefore adds a missing axis to the state model rather than a new subsystem.
- **Three axes describe every piece of state in the application.** Owner (chat, message, participant), audience (all participants, one participant, a role) and projection (prompt section, request tail, attachment, services only). Existing state is the case where the owner is the chat and the audience is everyone.
- **Private state is addressed through the invocation's ambient identity, never through an argument.** The invoking participant is resolved from the execution context, so a script cannot reach another participant's state by mistake or by intent. This is the same mechanism that already makes a command's own context available to its body.
- **Inbound privacy needs a lane that is not the transcript.** A character sheet must be present in the prompt of the participant who acts and absent from every other participant's view, so it cannot be persisted as an ordinary message. The existing request-tail context lane is exactly this: injected for one request, never persisted.
- **The prompt becomes a function of chat state and participant state.** That is the consequence of the previous claim, and it is the analogue of a stable shared prefix with a per-participant delta.
- **Outbound privacy is a distinct problem and is not a special case.** A reply may carry material addressed to one participant, and hiding it is the same operation as hiding a message part from an agent: the visibility check gains a participant as its reader, and a part carries an audience.
- **A record has two layers in one place.** Schema'd fields, where the declaring party fixes the shape and writes are validated against it, and free variables for scripts and notes. The schema is optional.
- **Audience is a predicate, not a role enumeration.** Self, all and the game master are instances of the same set membership, so no case about table role-playing is compiled into the mechanism.
- **A private attachment is an ordinary attachment with an audience.** Attachments are already the UI projection lane, so a per-participant panel needs an audience and nothing else.
- **An action raised by a private control executes under the identity of the participant it was rendered for.** Otherwise a button rendered for one participant would run as whoever happens to be looking.
- **The state survives restarts.** A session outlives a process, so the record is persisted per chat and participant rather than held in memory.

## Prerequisite

- A participant identity must be available on every request in the multiuser WebUI. Ownership, ambient identity and audience all depend on it, and none of them can be expressed without it.

## Not in scope

- Where the record is stored, whether per chat or per database, and how existing rows migrate.
- Slicing the work into implementable tickets; that is a separate step and a different vehicle.
- Participant-level settings inheritance. Settings inherit from application to profile to agent, and a participant level is a distinct axis that private state does not require.
- Hooks as the enforcement point for private state. That mechanism is a separate idea and must not become a prerequisite here.
- Deleting or exporting a participant's state, and what happens to it when a participant leaves a chat.

## Comments

- The inbound and outbound halves are separable and carry different risk. Inbound is what makes the use case work; outbound is what keeps it from leaking, and it becomes load-bearing as soon as a second real participant exists.
- The full visibility a game master needs is the argument for an audience predicate rather than a private-to-one rule.
