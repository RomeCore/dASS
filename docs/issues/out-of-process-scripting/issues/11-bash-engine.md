# Bash engine, interpreter discovery and the bash settings block

Status: done
Type: task
Blocked by: 08

## Question

The first non-Python language of the family: `BashScriptEngine` over the proven base, plus everything that makes
it usable on Windows, where the naive lookup finds the wrong bash. Covers the interpreter-discovery half of ticket
09 (moved here on 2026-10-25) and closes ticket 02.

Scope:

- the `BashScriptEngine` + descriptor (`.sh`, `#` frontmatter prefix, UTF-8 without BOM, LF);
- interpreter discovery in the documented order, with a configurable override;
- the bash settings block, next to the Python one;
- how "bash is missing" surfaces, given that the family has no load-time interpreter diagnostics;
- verification addons for the three scriptable kinds that matter.

## Answer

### Discovery

Order, first hit wins:

1. the configured path (`BashEnvironmentConfiguration.ExecutablePath`), when set;
2. Git Bash derived from `git.exe` on `PATH`: `dirname(git.exe)/../bin/bash.exe`, then `.../usr/bin/bash.exe`;
3. `bash` on `PATH`, **unless** its directory is `%SystemRoot%\System32` or contains `WindowsApps` - both are the
   WSL stub, and a WSL bash cannot see `C:\...` paths (neither the temp script nor the chat's working directory);
4. nothing - the engine reports why on the first run.

Candidates 2 and 3 are cached per process. Deriving Git Bash from `git` is not a nicety: on the development
machine git lives at `D:\Program Files\Git`, so every hard-coded `C:\Program Files\Git\bin\bash.exe` misses.

### WSL

Not supported in this change. It is recognised and named in the error text ("the bash on PATH is the WSL stub;
WSL is not supported - install Git for Windows or set the path"), never left to fail later as a `cd: /mnt/...`
confusion. Path translation for WSL is a separate effort.

### No load-time interpreter diagnostics

An addon's parse result is app-wide and cached, while the interpreter path is per-chat, so a load-time diagnostic
could not see it. Same as Python, which has no load-time interpreter diagnostic either: the settings check button
and the run are the two places that know a chat's settings.

Consequences:

- the spec's §12 loses "an interpreter that the engine cannot find" from the fatal list, and D20 stops promising
  load-time diagnostics (amended 2026-10-25);
- load-time fatals stay limited to what the file itself says: `json-input` against the language's capabilities,
  `json-output` on a context, `json-output` together with `run-terminal`, a malformed pattern;
- a missing interpreter surfaces as `ExternalProcessStatus.ProcessFailed` with the reason as the text: a tool card
  shows it, a command chip shows it, a prompt context logs a warning and injects nothing.

For the last one `ExternalProcessScriptEngineBase` gains one rule: building the launch parameters happens inside
the same `try` as `Launch`, so an engine that cannot find its interpreter returns a result instead of throwing
out of `RunAsync`.

### Settings block

`BashEnvironmentConfiguration : AdditionalEnvironmentSetting` (`[JsonDerived(typeof(AdditionalEnvironmentSetting), "bash")]`),
registered through `BashEnvironmentConfigurationProvider : IScriptEngineEnvConfigurationProvider`, rendered as a
second block on the chat environment page next to the Python one:

- one field - the executable path, empty meaning "discover it";
- a read-only "in use: `<path>` (`<version>`)" line filled by the check button, which runs `<effective path> --version`.

Showing what discovery actually picked is the cheapest answer to "Git Bash is installed, why does it not work".

### Engine

| Concern | Value |
|---|---|
| Extension | `.sh` |
| Frontmatter | `#` line prefix (proves ticket 02) |
| Encoding | UTF-8 without BOM |
| Line endings | LF - a CRLF script under MSYS fails with `$'\r': command not found` |
| Variables | `name='value'`, `'` escaped as `'\''`; JSON input mode passes the JSON as a single-quoted string |
| Working directory | from `ProcessLaunchParameters.WorkingDirectory` - no `cd` in the preamble (bash has no module resolution to fix, unlike Python's `os.chdir`) |
| Capabilities | `JsonOutput \| Terminal`, no `StructuredInput`, so `json-input: false` on `.sh` stays a load-time fatal |
| Invocation | `bash "<script>"` with the temp script path |

`Terminal` is declared up front and proved by `/sh-term` in a PTY. If Git Bash misbehaves under ConPTY, the
declaration is removed and `run-terminal: true` on `.sh` becomes a load-time fatal - that check compares the
frontmatter with the descriptor and knows nothing about the machine, so it does not contradict the rule above.

### Collateral

`ExtractJson`/`SplitPattern` in the engine and `CountUnescapedStars` in `FrontmatterBasedAddonParser` are two
implementations of "one unescaped `*`". The grammar moves into a single `ScriptOutputPattern` in core, used by
both, so the parser cannot accept a pattern the engine reads differently.

### Verification

- unit tests: the bash preamble and serializer (Cyrillic, single quotes, multi-line values, paths with spaces),
  and `ScriptOutputPattern` (`@@*@@`, `\*`, a missing marker, invalid JSON);
- manual: `tools/sh-echo.sh` (a tool), `commands/sh-files.sh` (a command with `json-output: "@@*@@"` over noisy
  stdout - two contracts in one run), `commands/sh-term.sh` (the terminal path);
- no bash prompt-context addon: the context path is already proven through the same adapter by `py-clock`, and a
  live bash context costs one process per prompt build.

## Comments

- Split out of the 2026-10-25 grilling. It takes over the discovery bullet of ticket 09 and closes ticket 02.

- **Landed 2026-10-25** (`9f7f8b84`), verified by hand on the development machine:

  | Check | Result |
  |---|---|
  | `shell-bash` (native) | Git Bash 5.2.37 via discovery; `/bin/bash` is gone |
  | PTY | `TERM=xterm-256color`, 80x30, interactive `read`, Cyrillic typed and read back |
  | `sh-echo` (`.sh` tool) | `#---` frontmatter parsed, script written to `%LOCALAPPDATA%\...\temp\scripts`, preamble variable present, `pwd` = the chat's working directory |
  | `sh-files` (`.sh` command, `json-output: "@@*@@"`) | part chip rendered from JSON between markers, noisy stdout ignored, a literal `@@` inside the payload survived (greedy capture) |
  | `sh-term` (`.sh` command, `run-terminal`) | countdown and an interactive `read` in the attached terminal |
  | unit tests | `BashScriptBuilderTests` (quoting, Cyrillic, LF) and `ScriptOutputPatternTests` (markers, escapes, greedy, five error paths) |

- **Authoring trap found by the pass**: a command's JSON contract is `part` / `generate` / `effect` / `error`, not the tool's
  `status` / `result` / `structured`. An addon written with the tool's shape renders **nothing at all** - there is no
  part and `generate: false` only lowers the intent, so the message silently ends up empty. Worth a line in the
  authoring skills (ticket 09).

- **Landmine**: the locator caches per configured path, so installing Git Bash *after* a failed lookup (empty
  configured path) keeps reporting "not found" until the app restarts.
