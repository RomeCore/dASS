# Scripting

A scriptable addon is **one file** in an addon pack. The folder decides what it is and the extension decides the
language:

| Folder | The file becomes |
|---|---|
| `tools/` | a tool the model can call |
| `commands/` | a `/name` slash command you run yourself |
| `context/` | a prompt context injected into every request |

| Language | Extensions | Frontmatter block | Runs where |
|---|---|---|---|
| Lua | `.lua`, `.alua` | `--[[` … `]]` | every build, in-process |
| Python | `.py` | `"""` … `"""` | Desktop build, separate process |
| Bash | `.sh` | `#---` … `#---` | Desktop build, needs bash (Git Bash on Windows) |
| PowerShell | `.ps1` | `#---` … `#---` | Desktop build, `pwsh` 7 or Windows PowerShell 5.1 |
| Batch | `.bat`, `.cmd` | `REM ---` … `REM ---` | Windows only |

The shell languages keep their frontmatter inside real comments, so the file stays runnable as-is.

## The frontmatter

```yaml
#---
#title: Count files
#description: Counts the entries of a directory and reports how many there are.
#category: Filesystem
#argument-schema: |
#  {"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}
#json-input: true
#---
```

| Key | Meaning |
|---|---|
| `title`, `description`, `category` | display strings; the description is what the model reads when it decides to call a tool |
| `argument-schema` | a JSON Schema **written as a YAML string** (`\|` block or quoted) - a one-line `{...}` mapping is not a string and is ignored |
| `behaviours` | what the body may touch; the approval policy gates the tool by these flags |
| `sync` | how the tool locks files: `write`, `read` or `none` (default derived from `behaviours`) |
| `json-input` | `auto` (default), `true` (a JSON string) or `false` (a native structure) |
| `json-output` | `true` (all of stdout is JSON) or `"<BEGIN>*<END>"` (a capture between two markers) |
| `run-terminal` | run the script in an interactive terminal instead of a managed process |
| `wait` | with `run-terminal`: whether the call waits for the script to exit (default `true`) |
| `timeout` | a prompt context's budget in milliseconds (default 2000, ceiling 10000) |

`json-input: false` on a language that cannot pass a structure (Bash, Batch) is refused at load.

## What the body receives

The variable names belong to the kind:

| Kind | Variable | By default | With `json-input: true` |
|---|---|---|---|
| Tool | `tool_args` | Lua table, Python dict, PowerShell `PSCustomObject`; Bash and Batch always get JSON text | JSON text |
| Command | `command_args` | the same, holding `{ positionals, keyed, rest, raw }` | JSON text |
| Context | `previous` | the previously injected text, or `nil`/`None`/`$null` | JSON text of it |

Bash and Batch have no structure of their own, so they always receive JSON text and decode it themselves
(`jq`, or whatever the script has).

## How the script answers

| Kind | The result |
|---|---|
| Tool | whatever it prints is the result; `json-output` takes a structure out of stdout on top |
| Command | the JSON contract `part`, `generate`, `effect`, `error` |
| Context | **stdout is the context text**; nothing printed means "no context" |

> [!WARNING]
> A command written with the tool's JSON shape (`status`/`result`/`structured`) renders **nothing**: there is no
> `part`, and `generate: false` can only lower the intent - the message silently ends up empty.

## Streaming, terminal and encodings

Long-running scripts can stream output, set status icons and progress bars through the `dass.tool.result` API - the UI
shows them live. With `run-terminal: true` the script owns an interactive terminal attached to the card instead of a
managed process: no timeout, and it can read from the keyboard.

Python and Bash scripts are written as UTF-8 without a BOM, PowerShell scripts as UTF-8 **with** a BOM (Windows
PowerShell 5.1 reads a BOM-less `.ps1` as ANSI), and the Batch preamble is pure ASCII so a Cyrillic argument survives
any code page.

See also: [Scriptable tools](scriptable-tools.md), [Tools](../tools.md).
