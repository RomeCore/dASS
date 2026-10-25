# Interpreter discovery, diagnostics, documentation and skills

Status: done
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

### Help pages

`docs/help/tools/scripting.md` (+ `ru-RU`) is rewritten around what a reader actually needs, in this order: which
folder makes a file a tool, a command or a context; the five languages with their extensions, frontmatter block and
where each runs; the frontmatter keys (`argument-schema`, `behaviours`, `sync`, `json-input`, `json-output`,
`run-terminal`, `wait`, `timeout`); what the body receives per kind in both input modes; and how each kind answers -
including the warning that a command scripted with the tool's JSON contract renders nothing at all.

`tools/metatools.md` is **replaced by `tools/scriptable-tools.md`** (both locales). The old page described "meta
tools" and pointed at `%LOCALAPPDATA%/LLMDesktopAssistant/metatools`, a folder that ticket 12 deleted; the new one
answers the question the old one existed for - which extensions are accepted, and what each needs to run - plus
where the files live now. The help viewer builds its tree from the embedded resource names and takes each title from
the first heading (`HelpDocumentStore`), so a rename needs no registration; only the inbound links had to move, and
one of them (`tools.ru-RU.md` → `../tools/metatools.md`) had been wrong for longer than this effort.

`getting-started.md` (+ `ru-RU`) lost its "Meta tools - dynamic tools written in Lua or Python" bullet, which had
been false since the rename, and gained the scripting page in "where to read".

### Authoring skills

All three learned the same three things, in their own vocabulary:

| Skill | What changed |
|---|---|
| `tool-authoring` | languages table (five engines, what each needs), the four scriptable flags, a `Bash, PowerShell, Batch` section (JSON text vs `PSCustomObject`, `json-output`, encodings the engine owns), the reserved temp folder instead of "the working directory", and three new traps |
| `command-authoring` | same table and flags, a shell section carrying the **command** JSON contract (`part`/`generate`/`effect`/`error`) and the warning that the tool's shape renders nothing, plus the `#`-in-YAML trap |
| `context-authoring` | same table, the "one process per prompt build" warning for shell contexts, `json-input` semantics for `previous` (`nil` stays `nil`), and the `#`-in-YAML trap |

The `meta` wording is gone everywhere: `ToolBehaviour.Scriptable` is what the parser adds, and the skills say so.

### AGENTS.md

The Desktop paragraph now lists four out-of-process languages and the interpreter-locator folders, and the addons
section gained a **Scriptable languages** table with the facts that cost a session to relearn: the frontmatter block
per language, the `#`-in-YAML comment trap, `argument-schema` needing to be a YAML string, `json-output`/
`run-terminal` being impossible for Lua, and a builder refusal landing as `ProcessFailed` because `BuildScript` runs
inside the guarded `try`.

### What was left out on purpose

The two paragraphs about settings blocks were already true before this ticket (ticket 11 built the bash one and the
PowerShell one came with the engine), and the interpreter-discovery bullet left this ticket for ticket 11 on
2026-10-25.

## Comments

- Landed 2026-10-26 together with the Batch and PowerShell engines, because a help page for four languages written
  before they existed would have been a guess.
- **Not verified in the running app**: help documents and skills are read from embedded resources, so the new pages
  appear only after a rebuild and a restart. The build is green; the rendering is the same path the previous pages
  used, and the tree is discovered from resource names.
- Deliberately not fixed here, recorded instead: `argument-schema` written as a one-line YAML mapping is dropped
  **silently** (the parser wants a scalar string, the diagnostic it raises is non-fatal, and `ScriptableToolParser`
  substitutes the empty default). The skills now warn about it; the code still says nothing.
