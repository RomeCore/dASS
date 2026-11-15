# message.user.submit.{direct,queue,edit}

Fires in the send pre-flight: before the draft is committed, before anything reaches the history and before anything joins the follow-up queue. The only point at which a refusal **keeps the user's text** in the input box.

| Event | Runs for |
|---|---|
| `message.user.submit.direct` | a send from the input box |
| `message.user.submit.queue` | a send that joins the follow-up queue instead of the history |
| `message.user.submit.edit` | a commit of an edited message |

A `hook-type` of `message.user.submit.*` covers all three. `message.user.*` also matches the insert points, and then the same hook fires twice for one UI send.

The variant names the method the user invoked, not the path the message took: a `direct` send joins the queue when one is already pending and stays `direct`.

The target is `{sender id}.{sender name}`, so `admin.Admin`, `admin.*` and `*.Admin` all match.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `chat_id` | number or nil | the chat |
| `message_id` | nil | always nil here: nothing is written yet |
| `index` | number or nil | the edited message index for `edit`, nil for a new message |
| `sender_id` | string | the login of the sender |
| `sender` | string or nil | the display name, nil when the login does not resolve |
| `content` | string | the text as typed: the `/command` token is still in it and `//` is not unescaped yet |
| `additional_content` | string or nil | the text the attached parts contribute to the model-facing content |
| `parts` | list or nil | `[{ kind, title }]`, where `kind` is `content`, `part`, `overlay`, `rail` or `data` |
| `visibility` | string | `always`, `reveal-after-send`, `only-users` or `only-agents` |
| `visible_to` | list or nil | the logins and agent ids the message is addressed to, nil for everyone |
| `is_whitelist` | bool | whether `visible_to` is a whitelist |
| `is_edit` | bool | whether the send commits an edit |
| `generate` | bool | whether the send asks for an answer |
| `command` | object or nil | `{ token, arguments, valid }`, nil when the message carries no token |

For an out-of-process engine these arrive as variables of the same names; with `json-input: true` the two structures are JSON text. `hook_type` and `hook_target` are separate variables in every engine.

## Verdict

| Field | Type | Effect |
|---|---|---|
| `decision` | string or nil | `disallow` refuses the send as unwanted, `error` refuses it as unworkable; `allow`, `none` and absent mean "no objection" |
| `reason` | string | the text the user sees as a toast; a generic sentence when omitted |

```lua
return { decision = "disallow", reason = "Not during the standup." }
return previous
```

## What the pipeline does with it

- `disallow` and `error` cancel the whole send: nothing is written, nothing joins the queue, the command does not run and no turn starts.
- The refusal reaches the user as a toast and **the draft stays in the input box** - the reason this point exists separately from `insert.pre`.
- An out-of-process hook that exits with code **2** refuses as well, with its error text as the reason.
- The chain replaces: each sequential hook returns the whole document. A hook with nothing to add returns `previous`.
- A send that passes still fires [message.user.insert.md](message.user.insert.md) `pre` right after it, so a hook subscribed to the whole family is asked twice.

## What bites

- A message that a script or the queue inserts never passes here: this point belongs to the user's own send. The universal gate is [message.user.insert.md](message.user.insert.md).
- The WebUI does not call the pre-flight yet, so a send from the browser is not gated here.
- `content` is raw. A guard that keys on `/tool:...` sees the token; the command has not run and may rewrite the message afterwards.
- `command.valid` is nil when there is no token, and always true otherwise - an invalid command is refused before the hook is asked.
- `decision = "deny"` is not a word. An unknown word becomes "no opinion".

## Neighbours

- The insert points: [message.user.insert.md](message.user.insert.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
