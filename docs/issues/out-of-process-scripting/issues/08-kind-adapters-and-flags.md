# Kind adapters and addon flags

Status: open
Type: task
Blocked by: 02, 05, 06, 07

## Question

Implement the three adapters and the flags they need (spec §3, §6, §8):

- `ExternalProcessToolEngine`, `ExternalProcessSlashCommandEngine` and `ExternalProcessPromptContextEngine` as
  the only implementations of the three kind interfaces; delete `PythonToolEngine`, `PythonSlashCommandEngine`,
  `PythonPromptContextEngine` and their descriptors;
- parse `json-input`, `json-output` (bool or pattern) and `run-terminal` in the three parsers, store them on
  `ToolInfo` / `SlashCommandInfo` / `PromptContextInfo`, and emit the fatal diagnostics of spec §12;
- build the kind's variables (`tool_args`, `command_args`, `previous`) and map the result per kind;
- expose `run-terminal` in the tool and command frontmatter.

## Answer

## Comments
