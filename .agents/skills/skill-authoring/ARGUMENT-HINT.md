# argument-hint grammar (dASS)

Disclosed reference for `skill-authoring`. The single home for how a skill's `argument-hint` frontmatter becomes the argument schema of its `/skill:<name>` command.

The authority is `SkillArgumentHintParser` under `src/LLMDesktopAssistant/SlashCommands/Arguments/`; read it rather than trusting this file when they disagree.

## What the hint becomes

`SkillParser` parses `argument-hint` at load time and stores the result on `SkillInfo.ArgumentSchema`. The derived `/skill:<name>` command binds its arguments against that schema. A skill with no hint - or a hint that falls back - still gets a command: the executor uses a **free-form `arguments` rest slot**, so the skill is always invocable.

## The shape

A hint is a list of bracketed arguments, whitespace between them optional (a bracket pair is already an argument):

```
<target> [mode:$fast|thorough] [extra...:%Extra context]
```

## Brackets decide required vs optional

| Bracket | Positional |
|---|---|
| `<name>` | **required** - the send is blocked without it |
| `[name]` | **optional** |

## Inside the brackets

An argument is a sequence of parts, in any order except the name, which must come first:

| Part | Syntax | Meaning |
|---|---|---|
| **name** | `name` | the argument's identity; letters, digits, `_` and `-`. Mandatory, and first |
| **rest** | trailing `...` | makes this the rest positional: the whole surplus, taken as **one verbatim value**, quotes kept |
| **choices** | `:$a|b|c` | the accepted values |
| **description** | `:%text` | the human-readable description |

Example with everything:

```
[destination...:$prod|staging:%Where to deploy]
```

- a missing or non-first name rejects the whole argument;
- a duplicated part (two names, two rests, two choice lists, two descriptions) rejects it;
- a **required rest** (`<name...>`) is rejected - a rest positional is never required;
- a **second rest** is rejected.

## Escaping

Inside a name, a choice or a description the reserved characters are escaped with a backslash:

`\\` → `\`   `\]` → `]`   `\>` → `>`   `\|` → `|`   `\:` → `:`   `\%` → `%`   `\$` → `$`

## Fallbacks - the parser never throws

| Hint | Result | Diagnostic |
|---|---|---|
| `null` / whitespace | free-form `arguments` rest slot | none |
| does **not** start with `<` or `[` | free-form rest slot whose description **is** the raw hint text | none |
| starts with `<`/`[` but does not parse | the same free-form fallback | `InvalidArgumentHint` |

The middle row is why a bare `argument-hint: "What would you like to learn about?"` works: it is prose, and it becomes the rest slot's description. The bottom row is the silent-failure trap: a typo inside the brackets throws your arguments away and only a diagnostic tells you.

## What the schema holds

- **Positionals** - one entry per declared positional, in order. Required ones carry `Required = true`; optional ones without a default keep their slot so indices stay aligned.
- **Rest positional** - the `...` slot, or none. Bound like any other argument (required / default / format all apply), but **never split**: the whole tail is a single value with quotes kept.
- **Choices** - `:$a|b` becomes a `ChoiceArgumentFormatProvider(choices, strict: true)`: it offers the values as completions **and** blocks the send on anything outside the set. With no choices the value is a plain string.

## How it reaches the command

The command's schema is `skill.ArgumentSchema ?? DefaultSchema`, where `DefaultSchema` is a free-form `arguments` rest slot. Binding runs before execution: a syntax error, a missing required argument, too many positionals or an invalid choice **blocks the send** with a `command.error.*` locale key, optionally pointing at the offending character.

## From bound arguments to the body

The bound arguments reach the skill body through `$variables` (`SlashCommandVariableExpander`):

- a named positional from the hint is exposed under its own name (`<target>` → `$target`);
- `$0 … $n` address positionals; the indexing is zero-based when the body contains `$0`, else one-based;
- `$ARGUMENTS` is the raw positional text and `$REST` the rest positional's text;
- `$SKILL_NAME`, `$SKILL_DIR` / `$CLAUDE_SKILL_DIR` cover the skill's identity;
- everything else resolves as a process environment variable, and an unknown name is left **verbatim**.
