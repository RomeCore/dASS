# 23: `/agent:<name>` executor

Status: ready-for-agent
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

- [ ] Sending `/agent:<name> input` launches the sub-agent; the task is attached to the message; the input reaches the
      sub-agent as its user message.
- [ ] `wait=false` returns immediately; `wait=true` awaits and injects the sub-agent's last generated content onto the
      message.
- [ ] The chat-level sub-agent tool policy is applied (through the resolver's policy override), not the caller agent's.
- [ ] An unknown sub-agent produces a user-facing error key instead of throwing.
- [ ] `wait` validates `true|false` (any other value blocks the send); locale keys added to `iv` and `ru-RU`; unit
      tests; the filtered suite stays green.

## Comments

<!-- appended conversation -->
