---
name: command-authoring
description: Write or amend a dASS scriptable slash command - the commands/<name>.lua|.py|.csx file in an addon folder - keeping its frontmatter contract (description, argument-schema, generate, model-facing) true to what the body does to the message. Use when creating a new slash command, editing or debugging one, wiring its arguments with the argument-schema DSL, or when the user asks how a command addon is shaped.
---

# Command authoring (dASS)

A scriptable slash command is a single file in a `commands/` folder of an addon source. A command is **user-only**: it is typed at the very start of a message and is never exposed to the model. It runs *before* the message reaches the model, and its whole job is to **mutate that message** - stamp a chip, rewrite the content, set the error.

Its frontmatter is a **contract with the user**, not the model: `description` and the argument names/descriptions are what the completion popup and the command cards show, `argument-schema` is what the user may type, and `generate` / `model-facing` decide what the model ends up seeing. The body is the implementation.

The contract is the part that breaks silently. A body that stops reading an argument, or an `argument-schema` that no longer matches what the body reads, turns a working command into a lying one, and nothing raises. Every run of this skill ends with the frontmatter and the body telling the same story - and with the command run once for real, because a command has no result panel and a wrong contract only shows up in the message.

## Steps

### 1. Find the file, or establish that it should exist

List the `commands/` folders of the addon sources and grep for the command name. Amending an existing command and writing a new one are the same job past this step - the only difference is whether there is a file to read first.

*Done when:* the path is known, or the command is established as new.

### 2. Read the contract before the body

Frontmatter first, code second. `description`, `argument-schema`, `generate` and `model-facing` state what the command promises; the body states what it does to the message. Most faults in existing commands are visible right here, before anything is touched.

*Done when:* both have been read and any gap between them is named out loud.

### 3. Change the command

Pick the language from **Languages** below. Keep the frontmatter and the body moving together:

- a new or changed argument → `argument-schema` moves with it, and so does every `command_args` read in the body
- the command should stop (or start) waking the model → `generate` moves with it
- the message should reach the model differently → `model-facing` moves with it

In an addon source the file name **is** the command name, so a rename means renaming the file.

*Done when:* the body runs, and every claim in the frontmatter is true of it.

### 4. Verify as far as the situation deserves

Verification is a judgment call, not a ritual. The moves, cheapest first:

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses and the name matches the file |
| Run the body through a runner with stubbed `command_args` | the logic works on the inputs you chose |
| Type `/name` | the command resolves, and its argument chips/hint match `argument-schema` |
| Run it | the executor runs and the part/content/error lands on the message |

Runners: `lua-execute` (Lua), `execute-python` (Python, plus `execute-python_venv_shell` for the venv), `csx-execute` (C# script). A runner hands you *its own* arguments, not the command's - so stub `command_args` yourself at the top of the snippet (a local table in Lua, a dict in Python, locals in C#), paste the body, watch the output. That is manual unit testing, and it catches most of what a first real run would catch.

`/addon-reload` rescans the packs after a file edit, without a restart. There is no stubbing layer around the real run: the arguments are bound by the very schema `argument-schema` produced.

The failure mode worth naming: claiming a command works because the file exists. A command has no result panel - if no chip or content change appeared on the message, the answer is "not verified yet", not "looks fine".

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

Order of preference: **Lua** - the only engine every build has, and the only one with the chat API. **Python** - for an external process, accepting desktop-only, no chat API and text-only output. **C# script** - when the user asks for it; it is in-process and reaches the domain model, but its API surface costs more than the command usually saves.

## The frontmatter

```yaml
--[[
name: my-command
title: My command
description: What this command does to the message. The user reads it in the popup and the cards.
category: Chat
aliases:
  - mycmd
generate: false
argument-schema: '<target> [mode:$fast|thorough]'
]]
```

Every engine wraps the same YAML in its own comment markers. `name` **must** equal the file name, or the command silently loads under a mismatched name.

- **`description`** is the user-facing pointer - it appears in the completion popup and the command card. A missing one raises a diagnostic.
- **`argument-schema`** - the arguments the user may type; see **Arguments** for its two forms.
- **`generate`** - `true` (default) leaves the caller's intent untouched, `false` forbids generation. It can only **lower** the caller's intent, never raise it. A command that only annotates usually sets `false`. This is the **ceiling**: a Lua body may lower it per run by returning `false` (see **What the body gets**).
- **`model-facing`** - how the message is shown to the model. `raw` (the default) sends the content as typed; `neutral` replaces it with the bare `/<token>` while every content part the executor appended still reaches the model - use it when the typed arguments are noise the injected content already carries; `hidden` drops the message from the model's view entirely.
- **`namespaces`** - extra qualifiers for the token; the source pack is appended automatically. The bare `/name` still resolves.
- Shared with every addon kind: `description` (missing → diagnostic), plus `order`, `aliases`, `enabled`, `hidden`. `title` and `category` are localizable display strings and drive no behaviour.

> [!NOTE]
> The key is `argument-schema` (dash), and its value is a hint or a DSL mapping - **not** the JSON schema a tool's `argument-schema` takes. Copying a tool's argument block into a command is the fastest way to an argument set that parses to nothing.

## Arguments

`argument-schema` comes in two shapes, told apart by the YAML **type** of the value:

**A scalar string** - the compact bracket hint (the same grammar as a skill's `argument-hint`): `<name>` is a required positional, `[name]` optional; inside the brackets `name...` is the rest positional, `:$a|b` lists choices, `:%…` is the description. Use it for a quick one- or two-argument command.

**A mapping** - the DSL, the only form that can declare **keyed** (`key=value`) arguments, defaults and types:

```yaml
argument-schema:
  positionals:
    - name: title
      description: Short title, a single token.
      required: true
  keyed:
    level:
      description: Severity.
      type: choice
      choices: [info, warn, error]
      default: info
    weight:
      description: A number.
      type: number
      default: "1"
    pin:
      description: A flag.
      type: boolean
      default: "true"
    tag:
      description: A free-form tag, absent when not given.
      type: string
  rest:
    name: text
    description: The free-form note body.
```

Per-argument fields: `name` (positionals/rest only - a keyed argument is named by its map key), `description`, `required`, `default` (a **string**, converted but never validated), `type` (`string` | `boolean` | `number` | `choice`) and `choices` (for `choice`).

Types arrive in the body converted: `choice`/`string` → string, `number` → number, `boolean` → boolean. A key neither supplied nor defaulted is **absent** (read it as nil/None). A `choice` is strict: a value outside the set **blocks the send before anything runs**.

> [!WARNING]
> **Keyed arguments must come LAST on the command line.** The parser takes everything before the *first declared key* as the positionals plus the rest tail, and everything from `key=` on as keyed. So the free text goes right after the positionals, and `key=value` sits at the end:
> ```
> /my-command <title> <free text...> level=warn weight=2.5 pin=false tag=release
> ```
> A key placed before the free text truncates the positionals/rest and the arguments you meant are silently gone. An **unquoted** keyed value runs to the next declared key (spaces allowed); `foo=bar` for an *undeclared* key stays plain text.

## What the body gets

**Lua** - in-process on AsyncLua, `await` works, **no stdout** (a command has nowhere to stream to).

- `command_args` - `{ positionals = {...}, keyed = {...}, rest = "...", raw = "..." }`
- `dass.chat` (the current chat) - `get_messages()` (a snapshot array of live `chat_message` handles), `get_title()`, `get_topic()`
- `dass.command.context.get_message()` - the handle of the message the command ran on (or `nil` outside a command)
- **the chunk's return value** - `return false` turns generation off (`SlashCommandExecutionResult.Generate`), `return true`, `nil` or no return at all leaves it as the frontmatter declared. Use it for the runtime decision a static key cannot express: "there is nothing to work on", "the user is offline", "the result is empty". Throw an `error(...)` instead when the run *failed* - that surfaces a message error, while `return false` is a clean, quiet stop.
- a `chat_message` handle: read/write `content`, `error`, `disabled_for_agents`; read-only `role`, `id`, `index`, `branch_index`, `branches_count`; methods `append_part(title, icon, color)`, `append_content_part(content, title, icon, color)` (the text also joins the model-facing content), `append_attachment_part(attachment, title, icon, color)`, `append_data(uicontrol, ...)`. `icon` is a `VisualIconKind` name (Material Icons), `color` a hex string; both optional.

> [!WARNING]
> **Never leave a function call as the last element of a table constructor** - the Lua engine here expands it into extra values and parentheses do **not** truncate it (measured; see `AR-0009`). Assign the call to a local first: `local meta = string.format(...); local lines = { header, meta }`. `{ header, string.format(...) }` injects the raw template and the arguments as junk elements.

**Python** - an external process, from a temp `.py` written into the working directory and deleted afterwards.

- `command_args` - a dict: `command_args["rest"]`, `command_args["keyed"]["weight"]`, ...
- **stdout is the whole result**: `print(...)` becomes one content part on the message. A non-zero exit code becomes the message error.
- **no chat API** - Python cannot read or mutate the message; it only prints. Its part is created by the engine with the command name as the title and **no icon**.

**C# script** - in-process, compiled on every run, full .NET access, **no stdout**.

- `CommandArgs` - `JsonNode` (the same `{ positionals, keyed, rest, raw }` tree)
- `Context` - the `SlashCommandExecutionContext`; `Context.Message` is the domain message you mutate directly
- `Workdir` - the chat's working directory

## Where the file goes

A `commands/` folder inside an addon source: `.agents/` or `.llmassist/` in a repo, `.llmassist/packs/<pack>/` for packs, `%LOCALAPPDATA%\.llmassist\` for machine-wide equipment. A command is always a **single file** (`commands/<name>.<ext>`); there is no folder format. The extension picks the engine. Deciding *which* command is worth writing at all is `equip`'s job; a command that contradicts its own contract but is otherwise fine is a `dass-report`.

## What bites

- A `name` that does not match the file name - the command may load under the wrong name and the bare token stops resolving.
- Keyed arguments placed before the free text - the positionals and the rest are silently swallowed.
- An `argument-schema` copied from a tool: the wrong key shape, the wrong value shape, or both, so the arguments parse to nothing.
- A `: ` (colon-space) inside a YAML description in a mapping - the frontmatter fails to parse. Prefer `-` over `:` in descriptions.
- Reading `description` as if the model saw it: a command is never exposed to the model, so `description` is for the user's popup and cards only.
- Expecting Python to reach the chat: it has no `dass.chat`/`dass.command`, and its part carries no icon.
- Returning a boolean from a **Python** or **C# script** command as if it were the Lua contract - only the Lua engine reads the chunk's return value; the other two always generate unless `generate`/the executor result says otherwise.
- Injecting content and then letting the model answer anyway - a command that fully replaces a model turn ends with `return false`.
- The trailing-call table-constructor trap in Lua (above) - a junk part that looks almost right.

## Neighbours

- The shape of a scriptable tool, and the shared engine table: the `tool-authoring` skill.
- The shape of a skill and the bracket-hint grammar in full: the `skill-authoring` skill.
- Commands and skills as LLT templates (`params_schema`, localization): the `llt-prompt-parts` skill.
- The craft of the writing itself: the `writing-for-agents` skill.
- Deciding *which* command is worth writing: `equip`. A contract that contradicts its own behaviour: `dass-report`.
