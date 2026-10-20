---
name: lua-script-authoring
description: Write or amend a dASS Lua script addon - the scripts/lua/<name>.lua file that loads into a chat's shared Lua state and publishes a namespace (`_NS`) that tools, slash commands and `lua-execute` then call. Use when extracting shared Lua helpers, when a tool and a command need the same code, when a script's namespace or manuals are wrong, when a script loads but its functions are missing, or when the user asks how the scripts/lua addon kind is shaped.
---

# Lua script authoring (dASS)

A `scripts/lua/` file is a **chat-level Lua library**, not a tool, not a command and not a prompt part. Its body runs **once per chat's Lua state**, before any tool or command script runs, and the only thing it can hand out is its **namespace table** (`_NS`). Everything else - a tool, a `/command` written in Lua, `lua-execute` - sees that table in `_G`, because they all execute against a shallow clone of the same globals.

The file's job is therefore narrow and worth naming out loud: **publish functions under a namespace, and say what they are via `manuals`.** A script that is not reachable as `someNamespace.fn()` did nothing at all.

## Steps

### 1. Decide that a script is the right kind

| You want | The kind is |
|---|---|
| the model to call it, with a schema and a result panel | a **tool** (`tools/*.lua`) |
| the user to type `/name` | a **slash command** (`commands/*.lua`) |
| code **shared by** a tool and a command | a **script** (`scripts/lua/*.lua`) |
| prompt text injected into the system prompt | a **skill** / prompt part |

A script is never invoked directly: it is not in the toolset, the model never sees it, and no token resolves to it. Its callers are other Lua bodies.

*Done when:* the caller(s) are named - which tool and which command will call `ns.fn()`.

### 2. Fix the namespace before writing the body

The namespace is the script's public API, and the one part that fails invisibly:

- it must be **reachable as Lua code** - `git`, `gitlib`, `pack.git`. Reserved words (`local`, `end`, `function`, `nil`, ...) produce a table that exists but can never be written as `local.git`; you would need `_G["local"]["git"]`, which nobody will do.
- do not take a root the native API already owns (`dass`, `fs`, `json`, `process`, `time`, `web`, `regex`, `icon`, `image`, `random`, `crypto`, `os`, `datetime`, `asynclua`) - the native parts are loaded first and a same-named namespace is a collision you will lose or corrupt.
- it should name the **domain**, not the caller (`git`, not `commitmessage`), because several callers share it.
- `namespace: ""` means "the global table" - almost always wrong for a helper.

*Done when:* `namespace` is a valid Lua identifier path, non-reserved, unprefixed by `dass`, and says what the code does.

### 3. Write the file

Markers are `--[[` ... `]]`, like every Lua addon, and `name` must equal the file name (minus the extension) - a mismatch raises `NameFSMismatch` and the script may load under the wrong name.

```lua
--[[
name: gitlib
title: Git helpers
description: Shared git collection and rendering used by the git-full-diff tool and the commit-message command. Publishes `git`.
namespace: git
manuals: |
  --- git — shared git helpers

  FUNCTIONS:

  --- async git.collect(repo) -> table
    Runs the git queries and returns { repo, branch, tracked, untracked, recent, scopes }.
    - tracked:   array of { path, add, del, binary }
    - untracked: array of { path, lines, binary, size, content }
    - recent:    array of "oneline" commit subjects
    - scopes:    array of scope strings collected from the last 500 commits

    --- git.summary_view_model(data) -> table
    Builds the dynamic view model for the diff summary panel.
]]

local function split_lines(s)
	if s == nil or s == "" then return {} end
	return regex.split("\r?\n", s)
end

local async function git(...)
	return await process.exec("git", { "--no-pager", ... }, { timeout_seconds = 180 })
end

_NS.split_lines = split_lines

_NS.collect = async function(repo)
	-- ... only what the callers cannot do themselves
	return { repo = repo, recent = {}, scopes = {} }
end
```

Rules the body has to satisfy:

- **publish through `_NS`** (`_NS.fn = ...`, `function _NS.fn()`). A top-level `local` dies with the chunk - the chunk's locals are not kept anywhere.
- **`_NS` exists only during the load.** Never read a global `_NS` later; assign it once, at the top level.
- **keep load-time work cheap and side-effect-free.** The body runs for every chat, on state creation and on every addon reload - not once per process.
- **`async` works** (`await` in the body and in functions the callers await). No stdout at load: `print` has no surface, use `dass.log.*`.
- **no `require` / `package` / `dofile` / `loadfile`.** The namespace table *is* the module system; a second script simply publishes another namespace.

*Done when:* every function a caller needs is reachable as `namespace.fn` after the load.

### 4. Give the namespace manuals

`manuals:` lands in the namespace's `manuals` table, is shown on the addon card and is what `print(manuals(git))` / `dass.help()` return. Write it in the same shape as the native namespaces: one `--- ns.fn(args)` per function, parameters, returns, then examples. This is the only documentation anyone (human or agent) can reach at runtime - a helper without manuals is a helper nobody will call twice.

*Done when:* `print(manuals(<namespace>))` prints the functions, not an error.

### 5. Verify

| Move | What it proves |
|---|---|
| `/addon-reload` then `lua-execute` with `print(type(ns))` | the file parsed, the namespace table exists |
| `lua-execute` calling one helper function | the functions work in the real chat state |
| the addon card in settings | name/namespace match, no diagnostic chips, manuals render |

`lua-execute` runs in the same chat's Lua state, so a loaded helper is directly callable there - that is the cheapest real test. Before the file exists you can still smoke-test the algorithm by pasting the body into `lua-execute` with `_NS = {}` stubbed at the top.

*Done when:* the level of checking is stated - which move was used and what it does not prove.

## Lifecycle, precisely

1. The chat-scoped `LuaService` is created on first use (a tool, a command or `lua-execute`).
2. Native API parts (`[LuaApi]` classes) load first and populate `dass.*`, `fs`, `process`, ...
3. Each effective script file runs its body as one chunk with `_NS` bound to its namespace table, then `_NS` is removed from globals.
4. A `--$`-prefixed line splits the body: everything **above** is the init chunk, everything **below** becomes a returned cleanup function, run when the state is reset (addon reload, settings change, chat disposal). A `--$` as the last line means "no cleanup".
5. The caller (tool / command / `lua-execute`) executes its own body against a **shallow clone** of the globals, so it sees every namespace but its writes do not leak back.
6. A body that throws at load is logged and skipped - the other scripts still load. That is why "the namespace is missing" is silent at the call site.

## What the callers get

| Caller | Its own globals | Can reach |
|---|---|---|
| tool (`tools/*.lua`) | `tool_args`, `dass.tool.result.*` | every namespace, `dass.chat`, `dass.ui` |
| command (`commands/*.lua`) | `command_args`, `dass.command.context.get_message()` | every namespace, `dass.chat`, `dass.ui` |
| `lua-execute` | nothing extra | every namespace (tool context is ambient) |

A namespace function can also call a tool by name - `await dass.tool.call("git-full-diff", { path = "." })` - which is a trusted, in-memory call: no policy, no confirmation, **no UI surface** (a form or a `append_data` panel from a nested call has nowhere to render). The result arrives as `content` (text), `structured` and `success`.

A command (or a tool) can raise an interactive terminal with `process.run_terminal(command, [args], { cwd, env })` and attach the returned `terminal` handle with `append_data()` on any host. The handle is not attached implicitly, and the process lives exactly as long as the attachment does - removing the attachment kills it.

## Settings and visibility

Scripts are the `AddonKind.LuaScript` kind, collected per chat (`ChatScriptSettings.Scriptset`, inherited app -> chat). Each one appears as a card with source, namespace chip, manuals block and an **enabled-only** toggle - there is no `hidden` mode for scripts, because they are never injected into a prompt.

Two consequences:

- **anything a helper provides can be switched off by the user.** A caller should fail with a readable message, not `attempt to index nil (global 'git')`.
- **the state is per chat**, so N open chats = N loads of every script.

## What bites

- **A namespace nobody can type.** `namespace: local` parses, loads, and is unreachable as `local.git`.
- **A namespace that shadows a native one.** The native part wins or gets clobbered; pick a domain name.
- **Top-level `local` as the API.** The chunk ends, the local is gone; only `_NS` survives.
- **Reading `_NS` at call time.** It is a load-time binding, removed from globals immediately after.
- **Load order between file scripts.** Only "native APIs first, then files" is guaranteed - resolve dependencies when the caller **runs**, not while loading.
- **A dependency on another script's namespace at load time.** That script may come later, may be disabled, may have failed to load.
- **`--$` as the last line** silently disables cleanup (the marker must have code after it).
- **Editing a script while another script is running.** A reload takes a non-blocking lock and is skipped if the state is busy - hit `/addon-reload` again rather than assuming the edit applied.
- **Expensive load-time work.** Body code runs per chat and per reload, including `process.exec`/`web.request` if you put them there.
- **`print` at load.** No output surface; `dass.log` is the only visible channel.
- **Long-bracket levels do not exist.** `[==[ ... ]==]` fails to parse - only the plain `[[ ... ]]` form is accepted, so inline text that itself contains `]]` has to be concatenated instead.
- **`return` on the same line as a call.** `print(x) return end` is a parse error in this parser; give `return` its own line, or separate the statements with `;`.
- **Claiming it works because the file exists.** The load error only shows in the log and on the card's diagnostic chip.

## Neighbours

- Scriptable tools and their frontmatter: the `tool-authoring` skill.
- Slash commands and their argument schema: the `command-authoring` skill.
- AXAML + dynamic view models (`dass.ui.create_control`, `append_data`): the `lua-ui-controls` skill.
- Live API surface: `print(namespaces())`, `print(manuals(dass, fs, process, dass.tool))`.
