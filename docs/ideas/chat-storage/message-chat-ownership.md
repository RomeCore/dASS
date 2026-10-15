# Where the chat ownership of a message lives

Status: open
Type: task
Blocked by:

## Requested

- A review of the chat storage schema as it stands.
- An answer to whether the message database model should carry the owning chat id.
- The reasoning of that review recorded as an idea ticket, so the question is not re-asked from scratch.
- The question of whether any change is needed at all is left open; the request is for an assessment, not for a schema change.

## Proposed design

- **Ownership is expressed once, by the node tree.** A node's parent is either another node or the chat id, and that edge is the only place a message is tied to a conversation; the message row is pure content with no references upward, unlike tool calls and additional chat data, which do carry their owner. Accepted as sound normalization.
- **A message belongs to exactly one node, and therefore to exactly one chat.** Appending, editing and branching all insert a new message row together with a new node, and message rows are never shared between chats. A chat id on the message row would consequently be write-once — never rewritten after the insert — which removes the usual drift argument against denormalizing it, but not the risk of a second source of truth.
- **Nothing reads messages outside a chat scope today.** Every reader already holds the chat id, so the field buys nothing at present, and the derived relationship is not worth duplicating for speculative callers.
- **The one real gap is the reverse lookup.** Resolving which node, and therefore which chat, owns a given message is a full collection scan, because the node's reference to its message is not indexed. Indexing that reference is the cheap fix, and it changes no schema.
- **If a chat-scoped flat query becomes necessary** — deleting, exporting, auditing, or gathering every row belonging to one chat without walking the tree — the owner id belongs on the message node rather than on the message: the node is already the unit that branching and selection address, and the node's edge to the chat already exists next to it.
- **A chat id on the message model is refused for now.** It duplicates a relationship that lives in the tree, and the single-writer invariant that keeps it honest only holds while no feature moves, forks or shares a message between chats. Refused rather than banned: the refusal is conditional on that invariant surviving.
- **A derived message-to-chat index collection is refused.** It carries the same maintenance cost as the field plus rebuild logic, and buys no invariant that the node field does not already give.
- **If a field is ever added, it must be stamped in the same transaction as the node**, never written afterwards by a follow-up update, and it must be filled in lazily on chat load. The storage layer has no migration mechanism, so a new integer column reads as zero on existing rows; a nullable field with an explicit "not yet stamped" state is preferred over a one-off backfill pass.
- **A chat id is unique per database file only.** In-memory chats reuse a fixed id, so the value must never be treated as a key that identifies a chat globally.
- **Whatever is chosen, the invariant is testable.** Walking one chat's tree and comparing the set of message ids found against the set obtained through the chat id is a small test that catches drift immediately.

## Not in scope

- Per-chat database files, and chat export or import formats. The current design is one shared store, and the field would only be a prerequisite for leaving it.
- Garbage collection of message rows whose node is gone. Transactions already commit the message and its node atomically, so such rows are not produced today, and the scenario only becomes possible once messages can be detached from the tree.
- Attributing token usage to a chat or a message. Usage records currently carry neither, and that is a separate decision.

## Comments
