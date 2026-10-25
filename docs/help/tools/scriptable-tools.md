# Scriptable tools

A **scriptable tool** is a tool defined by a file instead of by code: the file's frontmatter is the contract the model
reads, and its body is the implementation. The same file format also produces slash commands and prompt contexts - only
the folder changes.

## What is accepted

A tool is a single file in the `tools/` folder of an addon pack. The extension picks the engine:

| Extension | Engine | Needs |
|---|---|---|
| `.lua`, `.alua` | AsyncLua | nothing - runs in-process on every build |
| `.py` | Python | the Desktop build and a Python runtime or virtual environment |
| `.sh` | Bash | the Desktop build and a bash interpreter (Git Bash on Windows) |
| `.ps1` | PowerShell | the Desktop build and `pwsh` 7 or Windows PowerShell 5.1 |
| `.bat`, `.cmd` | Batch | Windows |

## Where the files live

Scriptable tools are ordinary addons, so they live in an addon pack next to every other kind:

- a `tools/` folder in the addon sources of a repository (for example `.agents/packs/<pack>/tools/`);
- `%LOCALAPPDATA%\.llmassist\packs\<pack>\tools\` for packs installed on this machine;
- the pack folders listed in the application settings.

Nothing needs to be registered: drop the file into a scanned `tools/` folder and the tool appears in the catalogue after
the addons are reloaded.

## Writing one

```yaml
--[[
title: Count files
description: Counts the entries of a directory and reports how many there are.
category: Filesystem
argument-schema: |
  {"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}
behaviours:
  - directory-read
--]]
print(#fs.list(tool_args.path))
```

The full contract - every frontmatter key, what the body receives, how the result is reported for each addon kind -
is on the [Scripting](scripting.md) page.

> [!TIP]
> The `argument-schema` has to be a **YAML string** (a `|` block or a quoted line). A one-line `{...}` mapping is not a
> string, so the tool silently ends up with an empty schema and the model has nothing to fill in.

See also: [Tools](../tools.md), [Scripting](scripting.md).
