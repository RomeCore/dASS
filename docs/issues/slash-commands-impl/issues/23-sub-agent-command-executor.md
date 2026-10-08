# 23: `/agent:<name>` executor

Status: resolved
Type: task
Blocked by: 21

## What to build

`/agent:<name> [input]` launches a predefined sub-agent from a message, mirroring the `agent-callsub` tool (see
[design ticket 04](../slash-commands/issues/04-skill-and-agent-command-semantics.md)).

- A **`SubAgentCommandExecutor`** (next to the provider) that resolves the source `SubAgentInfo`, builds **fresh**
  launch parameters (task name, the triggering chat and message, no initial messages), resolves them through
  `ISubAgentTaskParamsResolver` **with the chat-level sub-agent tool policy**, and launches through
  `IAgentTaskExecutor`. The task attaches to the command's message; a missing sub-agent (name drift) becomes a
  user-facing error key rather than a thrown exception.
- A **`wait` keyed boolean argument** (default `false`): `false` is fire-and-forget; `true` awaits the sub-agent and,
  on completion, injects its last generated content back onto the command's message as a content part.
- The executor returns `Raw` and leaves generation to the caller's intent.
- `SubAgentSlashCommandProvider` returns the real executor.

## Acceptance criteria

- [x] Sending `/agent:<name> input` launches the sub-agent; the task is attached to the message; the input reaches the
      sub-agent as its user message.
- [x] `wait=false` returns immediately; `wait=true` awaits and injects the sub-agent's last generated content onto the
      message.
- [x] The chat-level sub-agent tool policy is applied (through the resolver's policy override), not the caller agent's.
- [x] An unknown sub-agent produces a user-facing error key instead of throwing.
- [x] `wait` validates `true|false` (any other value blocks the send); locale keys added to `iv` and `ru-RU`; unit
      tests; the filtered suite stays green.

## Answer

Implemented the `/agent:<name> [input]` command:

- **`SubAgentCommandExecutor`** (next to the provider) builds fresh launch parameters, resolves them through
  `ISubAgentTaskParamsResolver` with the chat-level policy override, and launches through `IAgentTaskExecutor`. The
  task attaches to the command's message; a missing sub-agent becomes the `command.error.sub_agent_not_found` key
  instead of a throw.
- **`wait`** (the keyed boolean, default `false`): `false` is fire-and-forget and passes **no** command token (its
  release would cancel the background task); `true` awaits the sub-agent and injects its last generated content as a
  content part.
- **`SlashCommandBooleanFormatProvider`** validates, converts and completes `true|false`; the provider attaches it to
  `wait`.
- `SubAgentSlashCommandProvider` injects `ISubAgentTaskParamsResolver` / `IAgentTaskExecutor` / `IChatSettingsService`
  and returns the real executor. `StubCommandExecutor` stays only as the `SlashCommandInfo.Executor` default (no
  derived provider returns it any more).

Filtered runs: slash-commands **158 passed** (desktop build skipped at the maintainer's request).

## Comments

<!-- appended conversation -->
