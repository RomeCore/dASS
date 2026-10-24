# Preamble builders and per-language value serializers

Status: open
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

## Comments
