# session.start

Fires once when a chat has just been opened: the chat scope exists, every chat service has been constructed (the Lua state with it, so the chat's scripts have loaded), the history has been reloaded and the initial agents are in place.

Forced fire-and-forget: nothing waits for the hook, `execution-flow` is ignored and a verdict has nowhere to go.

The point has no target: `hook-target` cannot narrow it.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `session_id` | string | the runtime identity of this opening; the same chat opened again carries another one |
| `chat_id` | number or nil | the persisted chat |
| `title` | string | the chat title |
| `topic` | string | the chat topic |
| `message_count` | number | messages in the active branch - `0` for a freshly created chat, what it was reopened with otherwise |
| `created_at` | string | when the chat was created, ISO 8601 with offset; older than the session when the chat is reopened |
| `is_temporary` | boolean | the chat lives only in memory and is never persisted |

## Verdict

None. The chat is open and nothing is pending a decision.

## What the pipeline does with it

- Nothing: the event is informational. Whatever the hook changes, it changes through the `dass.*` APIs - a part on the chat, a value in `store.session`, a file.
- The chat scope stays alive for the rest of the session, so a hook may still be running when the user does something else. Nothing is ordered against it.

## What bites

- **Not once per application run.** Every opening fires it: switching back to a chat, creating a temporary chat, and the chat restored at startup all count. Keep state keyed by `session_id` when a run of the chat, not a run of the app, is what matters.
- A run-time registration (`dass.hook.register`) made by a script load **does** catch it: the Lua state is constructed, and its scripts loaded, before the opening handlers run.
- The chat is not on screen yet. Anything the hook writes to the UI may land before the user sees the chat.
- A temporary chat fires it with `is_temporary: true` and `chat_id` pointing at the in-memory chat - do not key persisted state on that id.

## Neighbours

- The other end of the same session: [session.end.md](session.end.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
