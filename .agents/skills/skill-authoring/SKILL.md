---
name: skill-authoring
description: Write or amend a dASS skill (a SKILL.md addon) - its frontmatter contract with the model and the user (user-invocable, disable-model-invocation, argument-hint, injection-mode, tool lists), and how the skill becomes a /skill:<name> slash command. Use when creating a new skill, editing or debugging an existing one, wiring a skill's arguments for its slash command, or when a skill's user or model reach must change.
---

# Skill authoring (dASS)

A skill is a single `SKILL.md` (or `<name>.md`) file in a `skills/` folder of an addon source. Where a scriptable tool's frontmatter is a contract with the **model**, a skill's frontmatter is a contract with **two clients**:

- the **model** reads `description` - through the `skill-load` tool and the skills section of the prompt - to decide whether to invoke the skill;
- the **user** reaches the skill through the `/skill:<name>` slash command, derived from the same addon, with the arguments the frontmatter declares.

The body is the skill's content. The contract is the part that breaks silently: a body that drifts from its `description`, or an `argument-hint` that no longer matches the variables the body reads, turns a working skill into a lying one, and nothing raises. Every run of this skill ends with the frontmatter and the body telling the same story.

This is the dASS-specific branch. The universal craft of writing the *content* - pointers, the information hierarchy, invocation choice, router skills - lives in `writing-for-agents`; how a skill becomes a template lives in `llt-prompt-parts`.

## Steps

### 1. Find the file, or establish that it should exist

List the `skills/` folders of the addon sources and grep for the skill name. Read [equip/FORMATS.md](../equip/FORMATS.md) for the roots and the short/full layout when the location is not obvious. Amending an existing skill and writing a new one are the same job past this step - the only difference is whether there is a file to read first.

*Done when:* the path is known (short format `skills/<name>.md`, or full format `skills/<name>/SKILL.md`), or the skill is established as new.

### 2. Read the contract before the body

Frontmatter first, body second. `description`, the two invocation gates and `argument-hint` state what the skill promises and how it is reached; the body states what it does and which `$variables` it reads. Most faults in existing skills are visible right here, before anything is touched.

*Done when:* both have been read and any gap between them is named out loud.

### 3. Change the skill

Keep the frontmatter and the body moving together:

- a new or changed argument → `argument-hint` moves with it, and the `$variables` in the body move with it
- the model's reach changes → `disable-model-invocation` and `description` move with it
- the user's reach changes → `user-invocable` moves with it
- the prompt cost changes → `injection-mode` moves with it

In short format the file name **is** the skill name, so a rename means renaming the file.

*Done when:* the body is coherent, and every claim in the frontmatter is true of it.

### 4. Verify as far as the situation deserves

Verification is a judgment call, not a ritual. The moves, cheapest first:

| Move | What it proves |
|---|---|
| Read it back | the frontmatter parses, the name matches the file/folder, and the gates read as intended |
| Type `/skill:<name>` | the command resolves, and its argument chips/hint match the `argument-hint` |
| Invoke it | the body injects, every `$variable` expanded, and the skill chip lands on the message |

After editing a file already on disk, `/addon-reload` rescans the packs without a restart. The runner is the live command itself; there is no stubbing layer, because the arguments are bound by the very schema `argument-hint` produced.

The failure mode worth naming: claiming a skill works because the file exists. If no `/skill:<name>` command appeared, the answer is "the loader did not pick it up yet", not "looks fine".

*Done when:* the level of checking is stated out loud, together with what it does and does not prove.

### 5. Show the result

The path, plus what was verified and by which move. The user's next action depends on that distinction.

*Done when:* the path and the verification level are on screen.

## Where the file goes

A `skills/` folder inside an addon source - `.agents/`, `.claude/`, `.gemini/` and the rest in a repo, `.llmassist/packs/<pack>/` for packs, `%LOCALAPPDATA%\.llmassist\` for machine-wide equipment. Extensions are `.md` and `.mdx`.

| Format | Path | Notes |
|---|---|---|
| Short | `skills/<name>.md` | the file name **is** the skill name |
| Full | `skills/<name>/SKILL.md` | the folder name **is** the skill name; the folder may carry resources |

`SkillFileLocator` is the authority; `equip/FORMATS.md` lists the roots.

## The frontmatter

```yaml
---
name: my-skill
description: What this skill does and when to reach it. This is what the model reads.
user-invocable: true
disable-model-invocation: false
argument-hint: "<target> [mode:$fast|thorough]"
injection-mode: default
allowed-tools: [fs-read_entry, fs-grep]
---
```

Frontmatter is **optional**: with none, the name falls back to the first `# heading` (or the file/folder name) and the description to the first paragraph. A missing description raises a diagnostic.

| Key | Default | Meaning |
|---|---|---|
| `name` | file/folder name | must equal the file (short) or folder (full) name, else a mismatch diagnostic |
| `description` | markdown fallback | the model-facing pointer; a missing one raises a diagnostic |
| `user-invocable` | `true` | whether a `/skill:<name>` command is derived at all |
| `disable-model-invocation` | `false` | forbids the model from invoking the skill; it also leaves the chat's available set |
| `argument-hint` | none | declares the command's arguments - see **Arguments** |
| `injection-mode` | `default` | `default` injects name + description into the prompt; `full` also injects the body |
| `allowed-tools` / `available-tools` / `disallowed-tools` | empty | tool lists shown in the UI. **Display metadata today, not enforcement** |

Shared with every addon kind: `order`, `aliases`, `enabled`, `hidden`, and the display strings `title` and `category` (localizable, no behaviour). `name` is validated by `SkillName`: 1-64 chars, digits / lowercase latin / hyphens only, no leading, trailing or doubled hyphens.

> [!NOTE]
> The two invocation keys split reach. A **model-invoked** skill keeps a `description` and no `disable-model-invocation` - the model may fire it. A **user-invoked** skill sets `disable-model-invocation: true` and turns `description` human-facing. A skill that is not `user-invocable` gets no command; a skill the user cannot remember is a candidate for a router skill (`writing-for-agents`).

## The command

Every valid, `user-invocable` skill yields a `/skill:<name>` command (namespaced by pack: `/skill:<pack>:<name>`). Two facts worth holding:

- The command's schema is the skill's `argument-hint`. **An invalid argument set blocks the send before anything runs.**
- The command injects the **full body**, with `injection-mode` ignored (it is a prompt concern), and the caller's generation intent untouched.

## Arguments and variables

`argument-hint` parses into the arguments the command binds. The full grammar - brackets, choices, descriptions, escaping and the fallbacks - is in [ARGUMENT-HINT.md](ARGUMENT-HINT.md); read it before writing a hint with more than a bare `<name>`.

Once bound, the arguments reach the body through `$variables`, expanded by `SlashCommandVariableExpander`:

| Variable | Resolves to |
|---|---|
| `$ARGUMENTS` | the raw positional text |
| `$REST` | the rest positional's text |
| `$0, $1, ...` | positionals; zero-based when the body contains `$0`, else one-based |
| `$<hint-name>` | the named positional from the `argument-hint` |
| `$SKILL_NAME` | the skill's name |
| `$SKILL_DIR` / `$CLAUDE_SKILL_DIR` | the skill's home directory |
| anything else | the process environment variable of that name |

An unknown variable is left **verbatim** - a typo must not silently eat text. A ` $name ` inside backticks (`` ` `` or ` ``` `) is left literal, for prose that talks *about* variables.

## What bites

- A `name` that does not match the file (short) or folder (full) name - the most common way a skill silently fails to load.
- An `argument-hint` that starts with a bracket but does not parse: it falls back to a free-form slot **and** raises `InvalidArgumentHint` - the arguments you meant are silently gone.
- A **required rest** positional (`<name...>`): rejected by the parser, so the whole hint falls back.
- Reading `disable-model-invocation` as "skill-load only": it also removes the skill from the model's available set entirely.
- A `.md`/`.mdx` file outside a `skills/` folder: nothing loads it, however correct the frontmatter is.

## Neighbours

- The shape of a scriptable tool: the `tool-authoring` skill.
- Skills and sub-agents as LLT templates (`params_schema`, localization): the `llt-prompt-parts` skill.
- The craft of the writing itself: the `writing-for-agents` skill.
- Deciding *which* skill is worth writing at all: `equip`. A contract that contradicts its own behaviour: `dass-report`.
