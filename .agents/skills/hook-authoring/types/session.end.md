# session.end

Fires once when the chat is closing: switching to another chat, closing the tab, or shutting the application down. The chat services are still alive when the handler is called, but they are disposed as soon as it returns.

Forced fire-and-forget: nothing waits for the hook, `execution-flow` is ignored and a verdict has nowhere to go.

The point has no target: `hook-target` cannot narrow it.

## Payload

The same document as `session.start`, with `message_count` being what the session produced rather than what it started with.

| Field | Type | Meaning |
|---|---|---|
| `session_id` | string | the runtime identity of this opening; the same chat opened again carries another one |
| `chat_id` | number or nil | the persisted chat |
| `title` | string | the chat title |
| `topic` | string | the chat topic |
| `message_count` | number | messages in the active branch at the moment of closing |
| `created_at` | string | when the chat was created, ISO 8601 with offset |
| `is_temporary` | boolean | the chat lives only in memory and is never persisted |

## Verdict

None. The chat is going away.

## What the pipeline does with it

- Nothing further in this chat: after the handlers the chat view model and the service scope are disposed, and `ChatExecutionTokenService` cancels every execution level around the same time. A turn in flight is already being stopped, so do not try to continue it.

## What bites

- **The scope dies right after the handler returns.** A hook that resolves a fresh chat service, starts a turn or awaits anything may find the chat gone. Use what is already in hand - `store.session`, files - and return fast.
- **A crash or a process kill runs nothing.** This is a courtesy, not a guarantee.
- A `fire-and-forget` body may be dropped with the chat. Since the point forces fire-and-forget anyway, treat a session hook as something that has to be quick rather than something that has to be awaited.
- Reopening the same chat is a new session: `session_id` differs, `chat_id` does not. A hook that accumulates per chat must not treat the pair as interchangeable.
- A chat that is never opened has no session, so it never fires - a chat sitting in the list is not a session.

## Neighbours

- The other end of the same session: [session.start.md](session.start.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
