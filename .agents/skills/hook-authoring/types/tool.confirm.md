# tool.confirm.{source}.{pending}

Fires at the confirmation stage, **inside** the synchronization lock: after the policy stage has produced a decision and before the stage acts on it - so before the user is asked and before the tool's own approval surface is reached. This is the only point where a hook may change the pending decision.

| Event | Runs for |
|---|---|
| `tool.confirm.chat-run.approve` | the agent's call the policy stage approved |
| `tool.confirm.chat-run.ask` | the agent's call the policy stage left to the user |
| `tool.confirm.chat-run.disallow` | the agent's call the policy stage refused |
| `tool.confirm.agent-task.*` | the same inside an agent task |
| `tool.confirm.*` | every confirmation, whatever the pending decision |

The `{pending}` suffix is `approve`, `ask` or `disallow` - the decision the pipeline arrived with. Only `chat-run` and `agent-task` reach this point: a user's re-run, a `/tool:<name>` command and a script call skip policy and confirmation entirely.

The `{pending}` suffix lets a hook target exactly one case instead of branching in its body: `tool.confirm.chat-run.ask` + `native.shell-*` is "a terminal call from an agent that is about to prompt the user".

## Payload

| Field | Type | Meaning |
|---|---|---|
| `tool` | string | the **bare** tool name, e.g. `fs-edit`; the origin-qualified name is in `hook_target` |
| `source` | string | `chat-run` or `agent-task` |
| `decision` | string | the pending decision: `approve`, `ask` or `disallow` |
| `reason_source` | string or nil | the layer that produced it: `approval-level`, `policy-mask`, `specifier`, `memorization`, `user`, `hook` |
| `reason` | string or nil | the explanation that came with it |
| `arguments` | object or nil | the effective argument set the call is about to run with, including a start hook's rewrite |
| `behaviour` | list or nil | behaviour flags in kebab case, refined by the preview stage |
| `chat_id` | number or nil | the chat the call belongs to |
| `agent_id` | string or nil | the agent whose message owns the call |

Every hook of one event receives the same payload: the pending decision is the pipeline's, not the previous hook's. What an earlier hook answered arrives in `previous`.

## Verdict

| Field | Type | Effect |
|---|---|---|
| `decision` | string or nil | answers instead of the user: `allow` approves, `ask` asks, `disallow` and `error` refuse; `none` keeps the pending decision |
| `reason` | string | why; becomes the model-facing text of a refusal, and the recorded explanation of a changed decision |
| `arguments` | object | replaces the argument set the call runs with |
| `persists_arguments` | bool | with `arguments`, also replaces the model's persisted intent |
| `override` | string | the deepest layer the verdict may pierce: `policy` (default), `memorization`, `all` |

```lua
-- approve a command the policy was about to ask about
return { decision = "allow", reason = "read-only git inspection" }

-- refuse without asking the user at all
return { decision = "disallow", reason = "shell commands are off in this chat" }

-- undo a memorized "no" for one specific case
return { decision = "allow", override = "memorization", reason = "the README is not a secret" }

return previous
```

## What the pipeline does with it

- `allow` and `ask` replace the pending decision with `Approve` and `Ask`, and the hook is recorded as its source. Turning a pending `ask` into an approval is the whole point of this point.
- `disallow` ends the invocation here with status **Rejected**: neither the user nor the tool's approval surface is consulted, and the model reads `reason`.
- `error` ends it with status **Error**: the call could not be carried out. Otherwise identical.
- `none` or no verdict keeps the pipeline's decision. `arguments` is applied either way, so a hook may rewrite the call while leaving the decision alone.
- An out-of-process hook that exits with code **2** refuses the call with status Rejected and its error text, without waiting for a verdict.
- The chain replaces: each sequential hook returns the whole document, and `previous` is the earlier verdict.

Lifting a pending **denial** is gated by `override`:

| The denial came from | Needs at least |
|---|---|
| the agent's policy, a specifier or a policy mask | `policy` (the default) |
| a decision the user memorized | `memorization` |
| a tool whose `ApprovalLevel` is `AlwaysDisallow` | `all` |

A verdict that may not pierce is logged and ignored; the call stays denied.

## What bites

- `allow` here is not `allow` at the start point: it skips the user. A hook that wants to stay out of the way returns nothing or `previous`.
- A generic auto-approver written for `tool.confirm.*` cannot undo a memorized "no" or an `AlwaysDisallow` tool by accident. Name the layer explicitly if that is the intent.
- The hook runs for every decision, including the ones already approved or refused. Put the case in the `hook-type` (`tool.confirm.chat-run.ask`) instead of branching in the body.
- A refusal is final: it is not re-routed to the tool's approval provider, and the tool never sees the invocation.
- An unknown `decision` word fails deserialization and means "no opinion" - the pending decision stands.
- The point runs while the call holds its synchronization lock. Keep the body short; a timeout is "no opinion", so a slow auto-approver silently falls through to the user.
- A Lua hook that calls a tool from here re-enters the pipeline as the `script` source, which skips confirmation - no recursion at this point, but the same hook still fires at start and result.

## Neighbours

- The start point: [tool.start.md](tool.start.md)
- The result point: [tool.result.md](tool.result.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
