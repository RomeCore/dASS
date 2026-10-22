---
name: context-authoring
description: Write or amend a dASS scriptable prompt context - the .lua / .alua / .csx / .py file in a context/ addon folder - keeping its frontmatter contract (kind, timeout, variability) true to where its text actually lands. Use when creating a new prompt context, when editing or debugging an existing one, when deciding between live-tail and supersede, or when the user asks how a prompt context addon is shaped.
---

# Prompt context authoring (dASS)

A scriptable prompt context is a single file in a `context/` folder of an addon source. It is **not an action**: the model never calls it and never sees it as an addon. It is a script that produces text which the prompt engine places somewhere in the request. Its frontmatter is a **contract with the prompt engine**: `kind` decides where the text lands, `timeout` decides how long the engine is willing to wait, and the body decides whether there is any text at all.

The contract is the part that breaks silently. A context with the wrong `kind` still runs and still produces text - it just lands in the wrong place, or persists a stamp the user never asked for. Every run of this skill ends with the frontmatter matching where the author actually wants the text.

## Steps

### 1. Find the file, or establish that it should exist

List the `context/` folders of the addon sources and grep for the name. Amending an existing context and writing a new one are the same job past this step.

*Done when:* the path is known, or the context is established as new.

### 2. Decide the kind before writing the body

This is the only real design decision, and it is made from the answer to one question: **what happens when the agent does not look again?**

| Kind | The text | Answer |
|---|---|---|
| `live-tail` | injected into the current request only, recomputed on every build, never persisted | "nothing - if it matters, it will be re-read" |
| `supersede` | persisted as a stamp on the agent's message, re-injected when the older stamp goes stale | "the agent must keep seeing it even after a compaction" |

Typical live-tail: time, branch, dirty files, environment probes - facts that are cheap to recompute and worthless once stale. Typical supersede: project rules the agent must not lose, a list it has to keep in mind across a long turn.

*Done when:* the kind is chosen, and the reason is one sentence.

> [!WARNING]
> `kind` **defaults to `supersede`**. Omitting it on a "current state" context silently turns it into a persisted stamp that re-announces itself whenever the text changes. Write it out.

### 3. Write the body against the output channel of the engine

Pick the language from **Languages** below. The channel is the whole contract on the body side:

- **only the collected text becomes the context** - nothing else
- empty or whitespace-only output means "no context this time": no tail, no stamp
- `previous` is the previously injected text (already wrapped, tags included) or empty - use it only in `supersede` bodies that want to compare for themselves; the engine already compares byte-exactly

Keep the body cheap. A live-tail runs on **every prompt build**, which is every tool round of every turn: a `git status` is fine, a network call is not.

*Done when:* the body runs, and every claim in the frontmatter is true of it.

### 4. Verify as far as the situation deserves

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses, `name` matches the file, `kind` is spelled right |
| Run the body in a runner | the logic produces the text you expect |
| Reload and look at the next request | the addon is picked up, the engine runs it, the text lands in the prompt |

Runners: `lua-execute` (Lua - `print(manuals(process))` and friends document the API in-app), `execute-python` (Python), `csx-execute` (C# script). A runner hands you its *own* globals, so stub `previous` yourself (`local previous = nil`), paste the body, watch the output.

The failure mode worth naming: claiming a context works because the file exists. If the addon source has not picked it up, the answer is "not verified yet".

*Done when:* the level of checking is stated out loud, with what it does and does not prove.

### 5. Show the result

The path, the kind, and what the model will actually see - the wrapped tag. Nothing else is visible to the user, so that snippet is the deliverable.

*Done when:* the path, the kind and the rendered tag are on screen.

## Languages

| Engine | Extensions | Frontmatter | Text channel | Runs where |
|---|---|---|---|---|
| Lua | `.lua`, `.alua` | `--[[` ... `]]` | `print(...)` | every build |
| Python | `.py` | `"""` ... `"""` | stdout | Desktop build only |
| C# script | `.csx` | `/*` ... `*/` | `Output.Write(...)` | every build |

Order of preference: **Lua** - the only engine every build has, and the cheapest to run. **Python** - when a library the Lua APIs cannot reach is needed, accepting desktop-only and process-per-build cost. **C# script** - when the user asks for it.

## The frontmatter

```yaml
name: git-status
kind: live-tail
description: The current git branch and the changed files of the working directory.
timeout: 3000
```

- **`name`** must equal the file name, otherwise the addon gets a name/file mismatch diagnostic. Allowed characters: letters, digits, `-`, `_` (up to 64). The name is also the XML tag the model sees, so keep it short and readable.
- **`kind`**: `live-tail` or `supersede`. Anything else is a fatal `InvalidKind` diagnostic and the addon does not load. Default: `supersede`.
- **`timeout`** - milliseconds, default `2000`, clamped to `1..10000`. Exceeding it kills the script and the context is simply absent. Set it above the default only when the body is genuinely slow.
- **`variability`** - optional display metadata (`configdriven`, `rare`, `frequent`, `perrequest`) shown as a card chip. Defaults: `perrequest` for `live-tail`, `frequent` for `supersede`. It drives no behaviour.
- **`description`** - what the user reads in the settings list. A missing one raises a diagnostic.
- Shared by every addon kind: `order`, `aliases`, `enabled`, `hidden`.

## What the body gets

**Lua** - in-process on AsyncLua, so `await` works.

- `previous` - the previously injected text (tags included) or `nil`
- `print(...)` - each call adds a line to the context text
- the chat's whole Lua API is available (filesystem, process, json, regex, ...); `print(manuals(namespace))` documents it in-app
- a runtime error means "no context", and a warning in the log

**Python** - a child process, from a temp `.py` written into the working directory and deleted afterwards.

- `previous` - a module-level global, `None` when there is nothing yet
- **stdout is the only channel**; stderr is not read and does not become the context
- a non-zero exit code means "no context", plus a warning in the log
- needs a Python runtime and the Desktop build

**C# script** - in-process, compiled on every build, no state shared between calls.

- `Previous` - the previously injected text or `null`
- `Output` - `Write(line)`, `WriteLines(lines)`, `SetContent(text)`, `Clear()`; the collected text is the context
- the return value is **not** the channel - write through `Output`

## Where the file goes

A `context/` folder inside an addon source: `.agents/` in a repo, `.llmassist/packs/<pack>/` for packs, `%LOCALAPPDATA%\.llmassist\` for machine-wide equipment. One file per context, and the extension picks the engine.

## What the model sees

The collected text is wrapped into an XML tag named after the addon (a leading digit gets an `_` prefix):

```xml
<git-status>
## tool-execution-v2...origin/tool-execution-v2
 M AGENTS.md
</git-status>
```

- `live-tail` text goes into the `<system-reminder>` of the current request, next to the pending assistant message
- `supersede` text is stored as a stamp on the agent's message and re-injected when the older stamp goes stale; a new stamp appears only when the rendered text differs **byte-exactly** - which is why a trailing newline is worth trimming: `print(x)` and `print(x, end="")` produce different tags

## What bites

- **`kind` omitted on a live-tail intent.** The default is `supersede`; the context silently becomes a persisted stamp.
- **Frontmatter delimiters copied between engines.** The three do not agree: `--[[ ]]`, `/* */`, `"""`.
- **`name` not matching the file name** - a diagnostic, and the addon is hard to find afterwards.
- **A trailing newline from `print`** - harmless for `live-tail` (a blank line inside the tag), but for `supersede` it is a guaranteed "change" the first time the script switches between `print` styles.
- **A slow body.** A live-tail runs on every build; an expensive probe quietly taxes every message.
- **Expecting chat APIs to matter.** A context cannot mutate the chat, cannot render UI and is invisible to `addon-search`; if the thing needs to *do* something, it is a tool or a slash command, not a context.
