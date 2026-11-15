# message.user.insert.{pre,post}

Fires around the write of a user message into the history, for **every** path: the input box, the follow-up queue, a command and a script.

| Event | Runs for |
|---|---|
| `message.user.insert.pre` | after the message is built and before it is written; may refuse the write |
| `message.user.insert.post` | right after the message reaches the history; nothing can be refused |

A `hook-type` of `message.user.insert.*` covers both, but the two are not the same power: `pre` decides, `post` observes.

The target is `{sender id}.{sender name}`, so `admin.Admin`, `admin.*` and `*.Admin` all match.

## Payload

The same document as [message.user.submit.md](message.user.submit.md), with these differences:

| Field | At `pre` | At `post` |
|---|---|---|
| `message_id` | nil | the database id |
| `index` | the edited index for an edit, nil for a new message | the 1-based history index |
| `generate` | nil | nil |
| `command` | nil | nil |

## Verdict

`pre` reads the same document as `submit`: `decision` (`disallow` or `error` refuse the write) and `reason`. `post` has no verdict - its return value is dropped.

```lua
return { decision = "error", reason = "The message is empty after the command." }
return previous
```

## What the pipeline does with it

- A refusal at `pre` leaves the history as it was: nothing is written, the command does not run and the turn does not start.
- For a message drained from the follow-up queue, a refusal **drops the entry**. Keeping it would fire the same refusal on every drain, forever.
- The refusal is **logged, not shown**: the draft is already gone and there is no surface left. A hook that must speak to the user belongs on [message.user.submit.md](message.user.submit.md).
- An out-of-process hook that exits with code **2** refuses as well.
- `post` is fired detached. A hook that reacts to the write may still be running when the next message lands.

## What bites

- A refusal here loses the user's text: it is committed before this point.
- `message_id` is nil at `pre`. An edit writes the message as a new branch at the end of the history, so `post` reports the new id and the tail index, not the edited one.
- `is_edit` is true for a commit of an edited message - the only case in which `pre` knows an index.
- `post` has no verdict: `return { decision = "disallow" }` there means nothing and is silently ignored.

## Neighbours

- The pre-flight that runs first for a UI send: [message.user.submit.md](message.user.submit.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
