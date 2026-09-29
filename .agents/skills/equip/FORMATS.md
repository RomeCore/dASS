# Addon formats (dASS)

Disclosed reference for `equip`. Every rule here is a lookup, and the lookup is cheap: when in doubt, read the locator named at the bottom rather than trusting this file.

Scriptable tools are the one exception to "everything is here": the shape of a tool - its frontmatter, its behaviours, what the body receives - lives in the `tool-authoring` skill, which is its single home. This file keeps only what `equip` needs in order to propose one.

## Where addons live

An **addon source** is a folder holding one subfolder per addon kind. Three roots hold sources, and the same folder names sit inside each of them:

| Root | Path | Use it for |
|---|---|---|
| Project | `<workdir>/<search-folder>/` - here, `.agents/` | Equipment specific to this repo - it travels with the code |
| User home | `%USERPROFILE%/<search-folder>/` | Equipment every repo on this machine sees |
| App | `%LOCALAPPDATA%\.llmassist\` | Machine-wide equipment: `skills/`, `agents/`, `templates/`, `packs/` |

`<search-folder>` is the agent-home family - `.agents`, `.claude`, `.gemini`, `.codex` and the rest. A **pack** is a nested source under any root: `<root>/packs/<pack>/`. Vendored collections live there - read them, leave them alone.

Inside a source, the subfolder decides the kind:

| Kind | Folder | File | Short format | Full format |
|---|---|---|---|---|
| Skill | `skills/` | `<name>.md` or `<name>/SKILL.md` | yes | `SKILL.md` |
| Sub-agent | `agents/` | `<name>.md` or `<name>/AGENT.md` | yes | `AGENT.md` |
| Scriptable tool | `tools/` | `<name>.lua` / `<name>.alua` / `<name>.py` / `<name>.csx` | yes | none |
| Template (LLT) | `templates/` | `.llt` - the extensions the LLT parser reports | yes | none |

Skills and sub-agents take `.md` or `.mdx`. A **full format** folder is the one that can carry resources next to its entry file; a scriptable tool is always a single file.

## Frontmatter

- Skills and sub-agents: `---` ... `---`, optional - without it the file falls back to plain markdown.
- Scriptable tools: **required**, and the delimiters belong to the engine. There is no markdown fallback, because the body is code.
- **`name` matches the file name** in short format, and **the folder name** in full format. A mismatch is the most common way a new addon silently fails to load.
- Skill keys: `allowed-tools`, `available-tools`, `disallowed-tools`, `injection-mode` (`default` or `full`). The tool lists are display metadata today, not enforcement.
- Shared by every addon kind: `description` - a missing one raises a diagnostic - plus `order`, `aliases`, `enabled` and `hidden`. `title` and `category` are localizable display strings: they name the addon in the UI and drive no behaviour.

## Scriptable tools, from here

One file per tool; the extension picks the engine: `.lua`/`.alua` (Lua), `.py` (Python - Desktop build only), `.csx` (C# script).

Language order when proposing one: **Lua first** - the only engine every build has, and the one with the least dependence on the machine. **Python second** - when a library the Lua APIs cannot reach is needed. **C# script last** - its API surface costs more than the tool usually saves.

A forged tool names its `behaviours` truthfully: those flags are what the approval policy gates the tool by. The rest of the contract, and the way to verify it, is in `tool-authoring`.

## The loadout notes

Lives next to the skill that reads them - `<workdir>/.agents/skills/equip/loadout/`, one file per equip run: `YYYY-MM-DD_<slug>.md` (created on the first run, if missing).

They sit in the tracked addon source on purpose: they are equipment of *this* repo, and a refusal reason that dies with the working copy is a refusal the next session repeats. The nested folder is inert - the skill loader reads `<folder>/SKILL.md` and nothing else.

No index. The addon sources already are the index of what the agent carries, the notes are the history of how it got there, and a grep across the two answers "do we already have this?".

```markdown
# YYYY-MM-DD - <the session in one line>

## Signals
- <signal> -> <candidate>

## Forged
- `<name>` - `<path>` - <what stops being manual>

## Refused / deferred
- <candidate> - <reason>
```

## Pointers

Read these instead of restating them here:

- The shape of a scriptable tool, its frontmatter, behaviours and verification: the `tool-authoring` skill.
- LLT prompt parts - components, slots, skills and sub-agents as templates, `params_schema`, localization: the `llt-prompt-parts` skill.
- Human-facing help pages: `docs/help/tools/metatools.md`, `docs/help/tools/scripting.md`.
- The folder and filename rules above come from `SkillFileLocator`, `SubAgentFileLocator`, `ToolFileLocator` and `TemplateFileLocator` under `src/LLMDesktopAssistant/`.
- The roots and the `<search-folder>` family come from `AddonPackSearchFoldersProvider`, `ChatAddonPackLocator` and `AppAddonPackLocator` under `src/LLMDesktopAssistant/Addons/Loading/`.
