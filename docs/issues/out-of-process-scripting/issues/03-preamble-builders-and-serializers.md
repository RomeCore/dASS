# Preamble builders and per-language value serializers

Status: done
Type: task
Blocked by: 01

## Question

For each of the four languages implement the preamble builder behind the language engine (spec §5, §5.1, §5.2):

- the engine + runtime part (encoding setup, `chdir` into the chat's working directory, PowerShell
  `[Console]::OutputEncoding`), the globals as one assignment per line, then the body;
- the value serialiser: Python literal (reuse `PythonStructuredSerializer`), Bash single-quote escaping
  (`'\''`), PowerShell single-quote escaping (`''`) or `ConvertFrom-Json` for native structure, Batch
  `set "name=value"` with `%` → `%%` and ASCII-escaped JSON;
- the document encoding per language: UTF-8 without BOM for Python and Bash, UTF-8 **with BOM** for `.ps1`,
  ASCII-only preamble for Batch;
- the Batch 8191-byte line limit as a fail-fast check (D25);
- `json-input` resolution against `ScriptEngineCapabilities`, with `Auto` falling back to a JSON string.

## Answer

### The four builders

| Language | Where | Document | Engine and runtime part | One variable |
|---|---|---|---|---|
| Python | `PythonScriptEngine.BuildScript` | UTF-8, no BOM, LF | `import sys, os`, stdout/stderr reconfigured to UTF-8, `os.chdir(...)` | `name = <python literal>` |
| Bash | `BashScriptBuilder` | UTF-8, no BOM, LF | none - the launcher owns the working directory | `name='<'json'>'`, `'` → `'\''` |
| Batch | `BatchScriptBuilder` | UTF-8, no BOM, **CRLF** | `@echo off`, `setlocal DisableDelayedExpansion` | `set "name=<json>"`, `%` → `%%`, ASCII-escaped JSON |
| PowerShell | `PowerShellScriptBuilder` | UTF-8 **with BOM**, CRLF | `if ($PSVersionTable.PSVersion.Major -le 5) { [Console]::OutputEncoding = [Text.Encoding]::UTF8 }` | `$name = '<json>'`, `'` → `''`, plus ` \| ConvertFrom-Json` for a structure |

Python is the one language whose preamble does chdir: it needs it for `sys.path[0]`-style resolution of an addon's
neighbours, while the launcher already gives every process the chat's working directory. Bash, Batch and PowerShell
have no equivalent need, so they add nothing.

### Why these particular forms

- **Batch with CRLF.** cmd misparses an LF-only file: `goto :label` and multi-line `( )` blocks break, and the
  failure looks like a bug in the addon. PowerShell is CRLF for the same "one way on every host" reason.
- **`@echo off` in Batch.** Without it, cmd echoes every command line - the preamble included - onto the stdout the
  caller reads, which breaks both the plain result and `json-output`.
- **`setlocal DisableDelayedExpansion`.** The launcher already passes `/d` (no AutoRun), and this keeps the claim
  inside the script: a value containing `!` must stay literal, which only holds while delayed expansion is off.
- **ASCII-escaped JSON for Batch, relaxed JSON for Bash and PowerShell.** A `.bat` is read in the console code page,
  so a readable Cyrillic literal would be mojibake on a machine with another code page - `\uXXXX` is the only
  code-page-independent form. The other two write UTF-8 documents and their scripts are read by a JSON parser, not by
  an HTML renderer, so `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` keeps the value readable.
- **PowerShell passes a structure as a `PSCustomObject` on every version** (D28): `-AsHashtable` exists from 6.0 and
  would give one addon two object models depending on the machine.
- **`[Console]::OutputEncoding` behind a version test, not a platform test.** 5.1 is the one that decodes a
  redirected stdout with the console code page; 7 already speaks UTF-8, and on Unix there is no code page to change.

### `json-input` resolution

`ExternalProcessScriptEngineBase.ResolveInputMode` answers `Auto` from the descriptor's capabilities
(`StructuredInput` → native structure, otherwise a JSON string) and refuses `Structured` for a language without it.
The refusal exists twice on purpose: at load time the parser turns it into a fatal diagnostic, and at run time the
engine returns `InvalidOutput` instead of quietly sending a JSON string to a script that expects an object.

### The 8191 limit (D25)

`BatchScriptBuilder.MaxLineLength = 8191`. Every generated `set` line is measured **after** the `%` doubling, and an
over-long one throws `InvalidOperationException` naming the variable and the length, instead of letting cmd truncate a
value the addon asked for. The body is never measured: those lines belong to the author, and policed limits we do not
generate would be guesses.

For the refusal to be a *result* rather than an exception out of `RunAsync`, both entry points of
`ExternalProcessScriptEngineBase` now build the document inside the guarded `try` - it used to sit above it, so any
builder exception escaped the caller. The status is `ProcessFailed` with the message as `Error`: `InvalidOutput` means
"the process ran and broke the output contract", and here the process never started.

## Comments

- Landed 2026-10-26 with the Batch and PowerShell engines (tickets 04 and 11 are the neighbours).
- The `ps-echo` sample lost half of its description to this: inside a `#`-prefixed frontmatter a `#` preceded by a
  space starts a YAML comment, so a value containing `#` has to be quoted or reworded. The bash builder has the same
  exposure - it is a frontmatter rule, not a language one.
- Unit tests: `BatchScriptBuilderTests` (echo/expansion guard, one `set` per variable, `%` doubling, ASCII escaping,
  inner quotes, CRLF, refusal above the limit, acceptance at it) and `PowerShellScriptBuilderTests` (apostrophe
  doubling, the version guard, both input modes, readable Cyrillic, CRLF).
