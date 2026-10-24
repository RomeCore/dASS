# Kind adapters and addon flags

Status: done
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
- expose `run-terminal` in the tool and command frontmatter;
- the Desktop **language registry** (moved here from ticket 01): resolve a file path to its descriptor and its
  `IExternalProcessScriptEngine`, so the adapters never build their own extension maps.

## Answer

Landed on 2026-10-25 (branch `tool-execution-v2`, uncommitted at the time of writing):

- `ExternalProcessToolEngine`, `ExternalProcessSlashCommandEngine` and `ExternalProcessPromptContextEngine` in
  `Desktop/Scripting/ExternalProcess/` are now the only implementations of the three kind interfaces. Each takes
  `ExternalProcessEngineRegistry` (which grew `Descriptors`, so an adapter never builds its own extension table).
- The three old Python engines and their four script providers/descriptors are **deleted**; `PythonHelperService`
  and the venv settings stay (D2).
- `ScriptFeaturePolicy` (`PlainOutput` / `JsonOutput` / `Terminal`, `Scripting/`) is declared by each kind parser and
  checked in the shared parser together with `ScriptEngineCapabilities` — two independent questions. A kind without
  `PlainOutput` gets a mandatory `json-output` (the future hook shape, unused today).
- `ToolInfo.ScriptLanguage` is deleted; `ScriptableAddonBase.Language` became the nullable `ScriptLanguage`.
- `ScriptEngineCapabilities.StructuredOutput` is renamed `JsonOutput`; the Lua descriptors keep `StructuredInput`
  only, so `json-output` / `run-terminal` on a `.lua` addon is now a load-time fatal.
- The parsers are the third arity (`FrontmatterBasedAddonParser<TAddon, TChange, TEngine>`) and own no extension
  tables; the descriptor cache key now includes `FrontmatterLinePrefix`, which it silently ignored before.
- `wait` is parsed into `ScriptableAddonBase.TerminalWait` (default `true`); `json-input` / `json-output` /
  `run-terminal` / `wait` all arrive through the shared parser.
- Result mapping lives in the adapters: tool `status` / `result` / `markdown` / `structured`, command `effect` /
  `generate` / `error` / `part` (explicit `content`; raw output becomes the part only without JSON), context no JSON.
  Unknown keys are ignored (D26 amended).
- Both projects build. Verification is manual.

## Comments
