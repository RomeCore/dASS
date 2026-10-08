# 22: `/skill:<name>` executor

Status: resolved
Type: task
Blocked by: 21

## What to build

`/skill:<name> [args]` injects the named skill's body into its own message, so the agent gets the skill in full without
a `skill-load` round-trip (see [design ticket 04](../slash-commands/issues/04-skill-and-agent-command-semantics.md)).

- A **`SkillCommandExecutor`** (next to the provider) that takes the source `SkillInfo` from the command, loads its
  body, substitutes the arguments, appends the home-directory note (same wording as the existing skill-load path) and
  adds an `AdditionalMessageContentPart` — non-restorable, chip "Used skill '{name}'".
- **`$ARGUMENTS` = the bound `RawPositionalArguments`.** A static `SlashCommandVariableExpander` exposes
  `string? GetSkillVariable(string name, SlashCommandBoundArguments arguments)`, resolving the built-ins (`ARGUMENTS`,
  `CLAUDE_SKILL_DIR` / `SKILL_DIR` = `HomeDirectory ?? dirname(Path)`, `SKILL_NAME`) and OS environment variables. Both
  `$NAME` and `${NAME}` forms are honoured; an unknown variable is left **verbatim**; the whole substitution logic
  lives in the expander, not the executor.
- The executor returns `Neutral` (the model sees the bare token, not the raw text) and leaves generation to the
  caller's intent.
- `SkillSlashCommandProvider` returns the real executor.

## Acceptance criteria

- [x] Sending `/skill:<name> args` inserts the message, injects the body in full (+ the home-dir note) and marks the
      part non-restorable; the chip shows "Used skill '{name}'".
- [x] `$ARGUMENTS` is replaced with the rest-positional text; `$CLAUDE_SKILL_DIR` / `$SKILL_DIR` / `$SKILL_NAME`
      resolve; a process environment variable resolves; an unknown `$VAR` stays verbatim; `${NAME}` works.
- [x] The model-facing content for the message is the neutral token (`/` + `RawToken`) with the body present, and the
      fingerprint records `ModelFacingMode.Neutral`.
- [x] Locale keys added to `iv` and `ru-RU`; unit tests for the expander and the executor; the filtered suite stays
      green.

## Answer

Implemented the `/skill:<name> [args]` command:

- **`SkillCommandExecutor`** (next to the provider) loads the skill body through the `ChatAgentSkill` adapter, expands
  its variables, appends the home-directory note (same wording as `skill-load`) and adds an
  `AdditionalMessageContentPart` (chip `command.skill.used`, non-restorable). It returns `ModelFacingMode.Neutral` and
  leaves generation to the caller.
- **`SlashCommandVariableExpander`** (static): `Expand(body, arguments, skill)` scans `$NAME` / `${NAME}`;
  `GetSkillVariable(name, arguments, skill)` resolves `$ARGUMENTS` (the rest positional), `$CLAUDE_SKILL_DIR` /
  `$SKILL_DIR` (`HomeDirectory ?? dirname(Path)`), `$SKILL_NAME`, then the process environment; an unknown variable is
  left verbatim.
- `SkillSlashCommandProvider` returns the real executor; `command.skill.used` added to `iv` and `ru-RU`.

Deviations / notes:

- `GetSkillVariable` takes the skill as a **required** third parameter (the maintainer's call): the skill-scoped
  variables cannot be resolved without it, and an optional parameter hid that.
- **Test-host deadlock found and fixed**: `AdditionalChatDataCollection` marshals collection changes to the Avalonia UI
  thread by default, and with no dispatcher loop in the test host two test classes doing that in parallel deadlock. The
  command-part tests raise the events synchronously (`RaiseInUIThread = false`).

Filtered runs: slash-commands **143 passed** (desktop build skipped at the maintainer's request).

## Comments

<!-- appended conversation -->
