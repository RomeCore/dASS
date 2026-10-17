# P9 — Tool-command source

Status: open
Type: task
Blocked by: 04

## Scope

A derived slash command per tool (spec §15, ticket 15 of the spec effort).

## Mechanics

- `DerivedSlashCommandProvider<ToolInfo>` sits next to the skill/sub-agent providers and owns
  `/tool:<name>` in the **derived** tier.
- The command's argument schema is generated from the tool's `ArgumentSchema`:
  - the designated main argument maps to the positional slot / rest that the slash-command argument
    grammar already supports;
  - every other property becomes a keyed argument;
  - required/optional and defaults are preserved.
- The executor runs the tool through the `ToolCommandOrchestrator` with `Source = Command`:
  streaming (Once) → validation → sync{ preview → execution } → settle. No policy, no confirmation; the
  hard floor (`AlwaysDisallow`) is honoured.
- The command produces a user message like the other derived commands (fingerprint, model-facing mode).
- The tool must exist in the chat's toolset; a disabled/absent tool yields no command.

## Acceptance

- `/tool:<name>` completes in the input and shows the argument hint.
- Running it executes the tool and appends the result to the chat.
- `AlwaysDisallow` tools are refused with a clear message.
- Unit test: schema mapping for a tool with a main argument, keyed optional arguments and defaults.
