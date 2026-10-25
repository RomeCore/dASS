# Out-of-process scripting engines — wayfinder map

`wayfinder:map` (the file name itself is the marker; there is no label system).

## Destination

An implementable, already-decided **spec** for one family of out-of-process script engines covering every
scriptable kind — tools, slash commands, prompt contexts, hooks later — with four languages in the first
release (Python, Bash, Batch, PowerShell), and the three existing Python engines dissolved into it.

The destination spec is [`spec.md`](spec.md). The design was settled in a single grilling session on
2026-10-24; the tickets below are **implementation slices**, not open questions.

## Notes

**Domain / skills to consult**

- `tool-authoring`, `command-authoring`, `context-authoring`, `skill-authoring` — the addon authoring contracts
  that this effort rewrites.
- `AGENTS.md` for the DI, addon and prompt-context conventions; `docs/issues/README.md` for this tracker.

**Standing preferences for this effort**

- **Everything out-of-process lives in the Desktop assembly.** Core keeps only the contracts its own parsers
  and providers touch (see spec §2).
- **No competing agent runtimes are mentioned** anywhere in this effort.
- The approved signatures in spec §4 are fixed: parameters may be **added**, never changed or removed. The
  only additions so far are `ScriptInputMode`, `ScriptOutputMode` and the terminal `Wait` flag.
- A promise in the frontmatter that cannot be honoured is a **fatal addon diagnostic at load**, never a silent
  downgrade at run time.
- Facts about the runtimes (Batch escaping and the 8191-byte line, the PowerShell 5.1 BOM, the launcher's
  default 30-minute timeout and its token linkage) live in the spec and must not be rediscovered by hand.

- **JSON mode reads `Stdout`; raw mode returns `Output`.** The merged stream the launcher already produces is the
  raw result — stderr is not filtered out of a script that has no JSON channel.
- **A handoff never contains a rollback recipe.** A wrong tree is fixed in place; pointing the next session at
  `git reset --hard` is how work gets lost.
- **The prompt-context budget lives at the call site**, in `PromptContextBudget` (`LLM/Services/Prompting/`), not
  in a language base — the pipeline decides how long a context may run, a provider only runs its body.

**Settled premises (decided at design time)**

- The input channel is a **generated preamble**; there is no input file, environment variable or argv.
- The variable **names belong to the kind**; a language engine only renders `name = value`.
- Structured input is a **capability** of the language, not a promise of the contract.
- `json-output` is the **per-kind result contract** (status, structured result, command part, later hook
  decisions) — not a choice about what text the model reads.
- Scriptable prompt contexts are reshaped to mirror tools and commands, and `PromptContextExecutionContext`
  replaces `EffectiveChatContext` in **every** provider, including anchored sections.

## Decisions so far

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->

- [Batch and PowerShell engines](issues/03-preamble-builders-and-serializers.md) - the last two languages, so the family
  now has all four builders and all four runners (ticket 04 closes with it). **Batch**: `.bat`/`.cmd`, `REM `-prefixed
  frontmatter, `@echo off` + `setlocal DisableDelayedExpansion`, `set "name=value"` with `%`→`%%` and ASCII-escaped JSON
  (code-page independent), CRLF (cmd misparses LF-only files), 8191 characters per *generated* line as a fail-fast, and
  cmd as the process - CreateProcess does not run a `.bat`, so `/d /s /c ""<path>""` with verbatim arguments.
  **PowerShell**: `.ps1`, `#`-prefixed frontmatter, UTF-8 with BOM (5.1 reads BOM-less as ANSI), `''` escaping,
  `ConvertFrom-Json` giving one `PSCustomObject` on every version (D28), `-NoLogo -NoProfile -ExecutionPolicy Bypass
  -File` with no `-NonInteractive` and no platform branch (`-ExecutionPolicy` is ignored off Windows), and
  `[Console]::OutputEncoding` behind `$PSVersionTable.PSVersion.Major -le 5`. Batch exists on Windows only and says so by
  claiming its extensions there only (D29); PowerShell is one engine for 5.1, 7 and Linux. `BuildScript` moved inside the
  guarded `try` of the runner, so a builder refusal lands as `ProcessFailed` instead of an exception.

- [Bash engine and interpreter discovery](issues/11-bash-engine.md) - `BashScriptEngine` over the proven base, the
  first non-Python language; discovery is the configured path, then Git Bash derived from `git.exe`, then a
  non-WSL `bash` from `PATH`, and `Terminal` is declared up front and proved in a PTY. **A missing interpreter is no
  longer a load-time diagnostic (D20 amended)**: the parse is app-wide and the path is per chat. The pattern
  grammar moved into one `ScriptOutputPattern`, whose capture is greedy (first begin marker to the last end marker).

- [Scriptable naming](issues/12-scriptable-naming.md) - `meta` left the tool and python surfaces: `ToolSource`,
  `ToolBehaviour` and `ToolBehaviourCategory` are `Scriptable`, the catalogue token and the Lua `tool.source` value
  follow, the python venv pair became `VenvActivateScriptPath` / `AddonVenvActivateScriptPath`, and the dead
  `%LOCALAPPDATA%\metatools` folder is gone. Only the R.I.P. epitaph keeps the word.

- The spec is [`spec.md`](spec.md) — all 27 decisions (D1–D27), the per-language preamble facts, the per-kind
  JSON contracts and the prompt-context refactor. The map is **closed**: the effort continues in the tickets
  below, which are implementation slices.
- [Prompt context refactor](issues/07-prompt-context-refactor.md) — the engine is a creator only
  (`CreateProvider`), **the provider is the run**, so the four script providers live next to their engines on one
  base class per language; `PromptContextExecutionContext` is composition (`Context`, `PromptContext`, `Chat`,
  `Services`) and replaced `EffectiveChatContext` in all five provider interfaces; `Kind` is parsed from the
  frontmatter; `EffectiveChatContext.Services` is gone; the helper keeps only the formatting rules. `Descriptors`
  stays with ticket 01. Both projects compile.

- **Kind adapters, flags and the terminal path landed** (2026-10-25): the three adapters
  (`ExternalProcessToolEngine` / `…SlashCommandEngine` / `…PromptContextEngine`) are the only implementations of the
  three kind interfaces, the three old Python engines are deleted, `ScriptFeaturePolicy` decides what a kind
  consumes (`PlainOutput` / `JsonOutput` / `Terminal`; no `PlainOutput` makes `json-output` mandatory — the hook
  shape, unused today), `ScriptEngineCapabilities.StructuredOutput` is renamed `JsonOutput`, the addon's `Language`
  is now `ScriptLanguage` and nullable, and `TerminalRun` + `RunInTerminalAsync` implement terminal mode. Both
  projects build; verification is manual. **D26 is amended: unknown JSON keys are ignored.** Lua does not honour
  `json-input` yet (a new ticket).

- **Contracts slice landed** (2026-10-24): `IScriptEngineDescriptor` + `ScriptEngineCapabilities`, `Descriptors`
  plural on the three kind interfaces, `Language` gone from them, the three kind descriptor interfaces deleted,
  parsers/locators resolving per file, and the frontmatter line prefix wired into `AddonParserDescriptor`. The
  Desktop language registry waits for the engine interface, so it moved to *Kind adapters and addon flags*.

## Not yet specified

- The hook decision shape (the JSON a hook handler answers with) — its own effort.
- The per-language runtime settings UI beyond the existing Python venv configuration.
- The JavaScript engine (enters by descriptor, nothing else changes).

## Out of scope

- **Sandboxing, permissions, resource limits** beyond a timeout.
- Long-lived workers, persistent interpreter sessions, REPL-style execution.
- Streaming managed-process stdout into a tool card.
- Scriptable **anchored** prompt contexts (the execution context already accommodates them, the engine does not
  build them yet).
- Migrating Python addons: none exist in the repository or its packs.
