# task.stop.{source}.{success,error,cancelled,blocked}

Fires once per launch of an agentic task, from the `finally` every exit path passes through, after the task's completion has already been settled for whoever was waiting.

| Event | Runs for |
|---|---|
| `task.stop.{source}.success` | the model ran and the task finished on its own |
| `task.stop.{source}.error` | the launch failed - `AgentTaskStatus.Failed`, which the word does not follow |
| `task.stop.{source}.cancelled` | the launch was cancelled, by the user, the cascade of a parent task or the task timeout |
| `task.stop.{source}.blocked` | a `task.start` hook refused the launch, so nothing ran |

A `hook-type` of `task.stop.*.*` covers every status.

Forced fire-and-forget: `execution-flow` is ignored, a verdict has nowhere to go, and a declared `timeout` is the only bound. The point cannot delay the caller that received the task's result.

The target is `{source id}.{task name}` - `agent-callsub.web-searcher`, `chat-naming.unknown`.

## Payload

The same document `task.start` receives, in its final state.

| Field | Type | Meaning |
|---|---|---|
| `task_id` | string | the task |
| `parent_task_id` | string or nil | the task that launched this one |
| `task_name` | string or nil | its display name |
| `source`, `source_id` | string | the launcher kind and its free-form id |
| `chat_id` | number or nil | the persisted chat |
| `hidden` | boolean | the task is an internal step |
| `triggered_sender` | string or nil | the sender of the message that triggered the task |
| `model`, `behaviour` | string or nil | the model of the launch and `normal`/`execute-once`/`only-response` |
| `launch` | string | `new` or `continue` |
| `status` | string | `success`, `error`, `cancelled` or `blocked` - the same word as the event suffix |
| `block_reason` | string or nil | why the launch was blocked, when it was |
| `error`, `error_type` | string or nil | the message and the type name of the exception that ended the launch |
| `message_count`, `initial_message_count` | number | messages the task holds, and how many were initial context |
| `feedback_iterations` | number | how many times the task continued through its feedback function |
| `sub_task_count` | number | sub-tasks it launched |
| `last_generated_content` | string or nil | the last content the agent produced |
| `usage` | object or nil | every launch of the task, cumulative |
| `new_usage` | object or nil | this launch only - nil when nothing ran |
| `started_at` | string | when this launch started, ISO 8601 with offset |
| `completed_at` | string | when it ended |
| `duration_ms` | number | `completed_at - started_at` |

`usage` and `new_usage` share the shape `input_tokens`, `output_tokens`, `cache_create_tokens`, `input_cache_hit_tokens`, `input_cache_miss_tokens`, `time_to_first_token`, `inference_time`, `execution_time`.

## Verdict

None. The launch is over; a blocking message or a returned document goes nowhere.

## What the pipeline does with it

- Nothing. Whatever the hook changes, it changes through the `dass.*` APIs or a file.
- It is called after `completed`, `completed_at` and the dispatcher's own end-of-task bookkeeping, so a hook that reads the task through the API sees it finished.

## What bites

- **`error` is `AgentTaskStatus.Failed`.** `failed` never matches: the status word and the enum name differ here, on purpose, to keep the vocabulary of the tool pipeline.
- **The await is already over.** The caller that waited for the task has its result before this hook starts; a hook that takes a minute delays nobody and changes nothing.
- **Not ordered.** Forced fire-and-forget means no `previous`, no chain and no order between two stop hooks, even if both declare `sequential`.
- **A chat closed mid-task kills the event.** The dispatcher is a chat service: close the chat while a task runs and the stop hook is skipped with a debug line, silently.
- `usage` is cumulative and `new_usage` is not. A continuation reports the earlier launches in `usage` - which the cost tracker has already counted - and only this launch in `new_usage`.
- A blocked launch reports `blocked`, `block_reason` filled, the same text in `error` (it is the exception a waiting caller received) and `usage`/`new_usage` nil: the task never spent a token.
- `last_generated_content` can be large, and it is the last assistant message only. Earlier messages and tool calls stay on the task.
- The status is repeated in the payload because a hook that matches `task.stop.*.*` has no other way to know the outcome - the same rule the tool result payload follows.
- A sub-task fired with `wait: false` can outlive its parent's stop. Counting starts against stops per task is safe; inferring that "the whole tree is done" from one stop is not.

## Neighbours

- The start of the same launch: [task.start.md](task.start.md)
- One agent's turn inside a chat: [chat.turn.agent.stop.md](chat.turn.agent.stop.md)
- The contract as a whole: [../SKILL.md](../SKILL.md)
