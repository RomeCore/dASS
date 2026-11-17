# task.start.{source}.{new,continue}

Fires once per launch of an agentic task, inside the background run: the task is already `Executing` and registered in the dispatcher, and its first request has not run yet. This is the gate - a refusal stops the launch before a single token is spent.

| Event | Runs for |
|---|---|
| `task.start.{source}.new` | a fresh task - `agent-call`, `agent-callsub`, `/agent:<name>`, a Lua `dass.agent.call`, an internal task |
| `task.start.{source}.continue` | a relaunch of a finished task (`agent-task_continue` here, `dass.agent.continue` in Lua) |

A `hook-type` of `task.start.*.*` covers both entries; `task.start.internal.*` covers every internal launcher.

Sequential by default: the launch waits for the hook, so that a hook with no `execution-flow` can refuse. Declaring `fire-and-forget` or `parallel` gives that power up - only a sequential verdict is read.

The target is `{source id}.{task name}`, for example `agent-callsub.web-searcher`. A task without a name carries `unknown` as the second segment, so the target is always two components.

**Only a task with a chat fires it.** The dispatcher is a chat service; a task launched with no `chat` has no hook surface at all.

## Payload

| Field | Type | Meaning |
|---|---|---|
| `task_id` | string | the task, stable across its launches |
| `parent_task_id` | string or nil | the task that launched this one |
| `task_name` | string or nil | the display name the launcher gave it |
| `source` | string | the launcher kind, the same word as the event: `internal`, `tool`, `script`, `manual`, `unknown` |
| `source_id` | string | the free-form launcher id: `chat-agent-call`, `agent-callsub`, `lua-api`, `command`, ... |
| `chat_id` | number or nil | the persisted chat |
| `hidden` | boolean | the task is an internal step, not a chat-visible one |
| `triggered_sender` | string or nil | the sender of the message that triggered the task |
| `model` | string or nil | the full name of the model the launch runs on |
| `behaviour` | string | `normal`, `execute-once` or `only-response` |
| `launch` | string | `new` or `continue` - the same word as the event suffix |
| `message_count` | number | messages the task holds, initial ones included |
| `initial_message_count` | number | how many of them were supplied as initial context |
| `feedback_iterations` | number | how many times the task continued through its feedback function |
| `sub_task_count` | number | sub-tasks launched so far |
| `last_generated_content` | string or nil | nil at a start, unless the task is being continued |
| `usage` | object or nil | cumulative usage of every launch so far |
| `new_usage` | object or nil | usage of this launch only |
| `started_at` | string | when this launch started, ISO 8601 with offset |
| `status` | nil | always nil at this point - the launch is running |

Both usage objects have the shape `input_tokens`, `output_tokens`, `cache_create_tokens`, `input_cache_hit_tokens`, `input_cache_miss_tokens`, `time_to_first_token`, `inference_time`, `execution_time`.

## Verdict

`decision` and `reason`, the same words the chat pipeline uses.

| `decision` | Effect |
|---|---|
| absent / `allow` | the launch runs |
| `disallow` | the launch is blocked: the task never reaches the model |
| `error` | same block, reported as a failure of the hook rather than as a decision |

A blocked launch ends the task as `blocked`, keeps `reason` as its `block_reason`, and fires `task.stop.{source}.blocked`. A caller that was waiting for the task (an `agent-call` with `wait: true`, its Lua equivalent) gets the reason as an error; a caller that did not wait finds it on the task.

An empty `reason` on a refusal is replaced with a generic text, never dropped: the awaiting caller must not receive a blank error.

## What the pipeline does with it

- Runs the sequential hooks in `order`, each receiving the previous verdict as `previous`; the chain replaces, so a hook with nothing to add returns `previous`.
- Blocks on the first non-empty blocking message (an out-of-process hook exiting with code 2, a Lua hook returning a second value), without waiting for the rest of the chain.
- On a block: sets the status to `blocked` and `block_reason`, settles the completion with the reason, skips the model entirely, and still fires `task.stop`.
- Nothing else: a start hook cannot rewrite the model, the tools or the initial messages.

## What bites

- **Per launch, not per task.** A task that is continued fires this event again with the same `task_id` and the suffix `continue`. A hook that keeps state must key it by `task_id` and handle the second start.
- **A follow-up into a running task is not a launch.** Pushing a message into a task that is still executing (`agent-task_continue` on a live task) enqueues it and fires nothing. Only a relaunch of a finished task produces `continue`.
- `fire-and-forget` or `parallel` cannot block, and a `parallel` verdict is dropped too. The gate is a sequential-only feature.
- A timeout, a crash or an unparseable return is "no opinion": the launch runs. A guard that fails open is the default, deliberately.
- Internal tasks fire here as well, so `task.start.internal.new` runs on the first message of every chat (`chat-naming`) and on every memory write. Narrow it with `hook-target: chat-naming.*` or accept the noise.
- The task is already visible in the dispatcher when this fires, and the awaiting caller is already waiting. A hook that takes two seconds delays the launch by two seconds; nothing else notices.
- Blocking here is not the same as blocking the tool. For a task launched by `agent-call`, `tool.start.chat-run` + `native.agent-call` refuses before the task exists; this point refuses after it exists and can see its name, model and messages.
- A blocked task still reports usage of `nil`: nothing ran, so there is nothing to bill.

## Neighbours

- The other end of the launch: [task.stop.md](task.stop.md)
- The tool call that launches most tasks: [tool.start.md](tool.start.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
