---
name: tool-authoring
description: Write or amend a dASS scriptable tool - the .lua / .alua / .py / .csx file in a tools/ addon folder - keeping its frontmatter contract (description, argument schema, behaviours) true to what the body actually does. Use when creating a new scriptable tool, when editing or debugging an existing one, when a tool's description no longer matches its behaviour, or when the user asks how a tool addon is shaped.
---

# Tool authoring (dASS)

A scriptable tool is a single file in a `tools/` folder of an addon source. Its frontmatter is a **contract with the model**: `description` is what the model reads to decide whether to call it, `argument-schema` is what it may pass, `behaviours` is what the approval policy gates. The body is the implementation.

The contract is the part that breaks silently. A body change that leaves the description alone turns a working tool into a lying one, and nothing raises. Every run of this skill ends with the frontmatter and the body telling the same story.

## Steps

### 1. Find the file, or establish that it should exist

List the `tools/` folders of the addon sources and grep for the tool name. Amending an existing tool and writing a new one are the same job past this step - the only difference is whether there is a file to read first.

*Done when:* the path is known, or the tool is established as new.

### 2. Read the contract before the body

Frontmatter first, code second. `description`, `argument-schema` and `behaviours` state what the tool promises; the body states what it does. Most faults in existing tools are visible right here, before anything is touched.

*Done when:* both have been read and any gap between them is named out loud.

### 3. Change the tool

Pick the language from **Languages** below. Keep the frontmatter and the body moving together:

- a new or changed argument → `argument-schema` moves with it
- new behaviour, or narrower behaviour → `description` moves with it
- anything new the tool touches → `behaviours` moves with it

In a short-format addon source the file name **is** the tool name, so a rename means renaming the file.

*Done when:* the body runs, and every claim in the frontmatter is true of it.

### 4. Verify as far as the situation deserves

Verification is a judgment call, not a ritual. The moves, cheapest first:

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses and the name matches the file |
| Run the body through a runner with stubbed arguments | the logic works on the inputs you chose |
| Call the tool itself | it loads, its schema accepts real arguments, and a result comes back |

Runners: `lua-execute` (Lua), `execute-python` (Python, plus `execute-python_venv_shell` for the venv), `csx-execute` (C# script). A runner hands you *its own* arguments, not the tool's - so stub the argument names yourself at the top of the snippet (a local table in Lua, locals in C#), paste the body, and watch the output. That is manual unit testing, and it catches most of what a first real call would catch.

The failure mode worth naming: claiming a tool works because the file exists. If the addon source has not picked the file up yet, the answer is "not verified yet", not "looks fine".

*Done when:* the level of checking is stated out loud, together with what it does and does not prove.

### 5. Show the result

The path, plus what was verified and by which move. The user's next action depends on that distinction.

*Done when:* the path and the verification level are on screen.

## Languages

| Engine | Extensions | Frontmatter | Runs where |
|---|---|---|---|
| Lua | `.lua`, `.alua` | `--[[` ... `]]` | every build |
| Python | `.py` | `"""` ... `"""` | Desktop build only |
| C# script | `.csx` | `/*` ... `*/` | every build |

Order of preference: **Lua** - the only engine every build has. **Python** - when a library the Lua APIs cannot reach is needed, accepting desktop-only and text-only results. **C# script** - when the user asks for it; its API surface costs more than the tool usually saves.

## The frontmatter

```yaml
title: My Tool
description: Describe what this tool does and when to use it.
category: general
approval-level: policy-based
behaviours:
  - file-read
argument-schema: |
  {
    "type": "object",
    "properties": {
      "input": { "type": "string", "description": "The input to process" }
    },
    "additionalProperties": false
  }
```

- **`description` is everything the model sees** when deciding to call the tool. Write when to use it, not only what it is.
- **`argument-schema`** is a JSON Schema object; the default is `{"type":"object","additionalProperties":false}`. Every argument the body reads belongs here, each with its own description.
- **`approval-level`**: `policy-based` (the recommended default), `policy-ask-or-disallow`, `policy-approve-or-ask`, `policy-auto-approve-unless-disallowed`, `policy-auto-disallow-unless-approved`, `always-approve`, `always-ask`, `always-disallow`.
- **`behaviours`** - kebab-case slugs, as a list, a comma-separated string, or a single value; the parser also accepts snake_case and raw enum names, case-insensitively. Values:

  `file-directory-create`, `file-read`, `file-edit`, `file-delete`, `directory-read`, `directory-edit`, `directory-delete`, `semantic-memory-read`, `semantic-memory-write`, `semantic-memory-delete`, `semantic-memory-clear`, `database-read`, `database-change`, `database-custom-connect`, `read-secrets`, `access-outside-workdir`, `workdir-change`, `clipboard-write`, `clipboard-read`, `internet-access`, `long-running-task`, `execute-external-process`, `possibly-unexpected`, `run-terminal`, `user-interaction`, `agent-execution`, `addon-pack-edit`, `prompt-edit`, `script-edit`.

  What each one means is in `src/LLMDesktopAssistant/Tools/ToolBehaviour.cs`. The `meta` flag is added by the parser on top of whatever the tool declares.

  **This is the security-relevant field.** The approval policy gates the tool by these flags, so a tool that understates what it touches walks past the user's own auto-approve and disallow settings while looking perfectly correct.

- Shared by every addon kind: `description` - a missing one raises a diagnostic - plus `order`, `aliases`, `enabled` and `hidden`. `title` and `category` are localizable display strings and drive no behaviour.

## What the body gets

**Lua** - in-process on AsyncLua, so `await` works.

- `tool_args` - arguments as a table: `tool_args.input`
- `print(...)` adds an output line; `return value` becomes the structured result
- `dass.tool.result` (raw global `_dass_tool_result`) - streaming: `set_status(icon, title)`, `write(...)`, `set_progress(current, min, max)`, `use_markdown(true)`, `complete_with_success()`. Icon names come from Material Icons.
- `_dass_tool_ctx` - the execution context
- a runtime error returns as a failed result carrying its message
- the API documents itself in-app - `print(manuals(asynclua))` is the pattern the `lua-execute` tool advertises - and the surface lives in `src/LLMDesktopAssistant/Scripting/Lua/API/`

**Python** - an external process, from a temp `.py` written into the working directory and deleted afterwards.

- `tool_args` - arguments as a dict: `tool_args["input"]`
- **stdout is the return channel**: `print(...)`. Exit code 0 means success; anything else comes back as a failure with the process output attached.
- no structured result and no streaming - text only
- needs a Python runtime or virtual environment, and the Desktop build

**C# script** - in-process, compiled on every call, no state shared between calls, full .NET access.

- `ToolArgs` - arguments as `JsonNode`: `(string?)ToolArgs?["input"]`
- `Result` - `Write`, `SetStructured`, `SetStatus`, `SetProgress`, `UseMarkdown`, `CompleteWithSuccess`
- `Context` and `Workdir` are available too
- the globals are declared in `src/LLMDesktopAssistant/Scripting/CSX/CSharpScriptGlobals.cs`

## Where the file goes

A `tools/` folder inside an addon source: `.agents/` in a repo, `.llmassist/packs/<pack>/` for packs, `%LOCALAPPDATA%\.llmassist\` for machine-wide equipment. A tool is always a single file, and the extension picks the engine. Deciding *which* tool is worth writing at all is `equip`'s job; a tool that contradicts its own contract but is otherwise fine is a `dass-report`.

## What bites

- Frontmatter copied between engines: the three do not agree on delimiters.
- A `.py` tool on a build without the Python engine: nothing loads it, and the diagnostic talks about extensions rather than about the platform.
- An argument read by the body but absent from `argument-schema`: the model cannot pass it, and nothing warns.
