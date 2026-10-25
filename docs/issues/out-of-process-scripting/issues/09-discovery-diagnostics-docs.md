# Interpreter discovery, diagnostics, documentation and skills

Status: open
Type: task
Blocked by: 11

## Question

Finish the surface around the family (spec §12):

- interpreter discovery with the documented order (Git Bash before WSL and WSL only when configured; `pwsh`
  before `powershell.exe`; `py -3` → `python` → `python3`; `cmd.exe`), reporting a missing runtime as a
  **load-time** addon diagnostic;
- rename the Python "meta tool environment" setting to the scriptable-addon environment — one venv path for
  every scriptable kind, no routing by kind;
- rewrite `docs/help/tools/scripting.md` and its `ru-RU` counterpart, plus the metatool page that lists the
  accepted extensions;
- update the `tool-authoring`, `command-authoring` and `context-authoring` skills for four languages, the
  preamble variables, the two mode fields and `run-terminal`;
- update `AGENTS.md` where it still describes the scriptable kinds as Lua and Python only.

## Answer

## Comments

- **2026-10-25:** the two settings bullets left this ticket. Interpreter discovery moved to ticket 11 (it belongs
  with the bash engine, which is its first non-Python consumer), and the "meta tool environment" rename moved to
  ticket 12. What remains here is documentation and the authoring skills. The load-time diagnostic for a missing
  runtime is cancelled outright: it belongs to the chat's settings, which an app-wide parse cannot see - see
  ticket 11.
