---
name: llt-prompt-parts
description: Authors and edits dASS prompt-part templates in LLT (.llt) - components, system/persona/specialization slots, template-based skills and sub-agents, including params_schema parameterization and localization. Use when the user asks to create, change, translate or review .llt prompt parts or prompt parts inside addon packs.
allowed-tools: [fs-read_entry, fs-grep, fs-glob, fs-edit, fs-write_file, fs-create_directory]
---

# LLT prompt parts (dASS)

Creating and editing dASS **prompt parts**: LLT templates with `type:` ∈ `component | slot | skill | sub-agent`,
stored in addon packs (`templates/*.llt`).

## Scope

**In scope:** user/pack `.llt` prompt-part templates.

**Out of scope (do not touch):** built-in system templates (`src/LLMDesktopAssistant/Prompting/Resources/*.llt` -
system prompt, router, summarizer, memory_*, naming, image_describer, sliders, etc.).
Editing those is a separate process (embedded resources, parsed at app startup). You may read them **as a reference**.

`type: 'slider'` is **legacy** and being removed. Behaviour sliders are replaced by a system component + `params_schema`.

## Where the template lives

`TemplateFileLocator`: the **`templates/`** folder, extension **`.llt`**, files stored **flat** -
`templates/<name>.llt` (a subfolder per template is not supported; only skills are laid out that way).

Pack roots (a `templates/` folder is looked up inside each of them):

| Pack | Path |
|---|---|
| AppData | `%LOCALAPPDATA%\.llmassist` → `…\.llmassist\templates\*.llt` |
| User agents home | `%USERPROFILE%\<search-folder>` , where `<search-folder>` ∈ `.llmassist`, `.agents`, `.claude`, `.gemini`, `.github`, `.codex`, `.cursor`, `.openclaw`, `.windsurf`, `.roocode`, `.cline`, `.opencode`, `.lmstudio`, `.junie`, `.everywhere` |
| Packs | `%LOCALAPPDATA%\.llmassist\packs\<pack>\templates\*.llt`, `%USERPROFILE%\<search-folder>\packs\<pack>\templates\*.llt` |
| Current workdir | `<workdir>\<search-folder>\templates\*.llt` (usually `<workdir>\.agents\templates\*.llt`), plus the workdir itself if `UseWorkingDirectoriesAsPacks` is enabled |

Notes:

- Deduplication by file name is **disabled** for templates - the file name is free-form (make it meaningful).
- A single file may contain **several** `@template` blocks.
- A parse error turns the **whole file** into a diagnostic addon - every template in it disappears. The file must parse.
- Changes are picked up **without restarting the app** (addons are reloaded on the next chat run / toolset access).

## LLT syntax (cheat sheet)

### Headers

```llt
@template my_id { … }             @/ text template → string
@messages template my_id { … }    @/ message template → list of role/content
```

For prompt parts the body renders as a **string**: `EffectiveTemplate.Render(context, functions).ToString()`.
Hence the rule: a prompt-part body is always a **text** `@template`. `@messages template` inside a part body yields
garbage (render returns a message list, not a string).

### Metadata (first thing in the body, constants only)

```llt
@metadata
{
	guid: '3a4b5c6d-7e8f-9a0b-1c2d-3e4f5a6b7c8d',
	lang: 'ru-RU',
	type: 'component',
	title: 'My component',
	category: 'safety',
	description: '…',
	params_schema: { … }
}
```

Keys go **without quotes**, string values in single quotes. Expressions and variables inside `@metadata` are forbidden.

### Statements

| Construct | Syntax |
|---|---|
| Condition | `@if cond { … } else if cond { … } else { … }` |
| Loop | `@foreach item in items { … }` (the loop variable does not leak outside) |
| While loop | `@while cond { … }` |
| Local variable | `@let x = expr` (lexical scope) |
| Assignment | `@x = expr` (to an existing variable) |
| Template insertion | `@render 'other_id'`, `@render 'other_id' with expr` |
| Output | `@expr`, `@(expr)`, `@expr:'format'` |
| Comments | `@/ to end of line`, `@* block *@` |
| Escapes | `@@` → `@`, `{{ ` → `{`, `}}` → `}` |
| Raw text | wrap a fragment in five backticks (a line of five backticks before and after) |

### Expressions

```llt
@name                      @/ property of the root context; throws if the property is absent
@if ?name { … }            @/ safe access: null instead of an error (for output use @(name ?? ''))
@if user ?: 'name' { … }   @/ "has property" operator: distinguishes absent from null
@a.b.c   @a?.b             @/ chains and safe navigation
@a[expr]   @a?[expr]       @/ indexing
@a.method(args)   @func(args)
@#items                    @/ length (unary #)
@(price * count)           @/ binary expressions - MUST be parenthesized
@(user ?: 'name' ? user.name : 'Anonymous')   @/ ?: - the "has property" operator
@(nick ?? 'Anon')          @/ ?? - null OR absent
@(flag ? 'yes' : 'no')     @/ ternary
```

Literals: numbers (`42`, `3.14`, `-7`), strings (`'…'`, `"…"`), `true`/`false`/`null`,
arrays `[1, 2, 3]`, objects `{ name: 'Fish', qty: 5 }` (a key may be quoted or an `[expr]`).

Precedence: `* / %` → `+ -` → `< <= > >=` → `?:` (has) → `== !=` → `&&` → `||` → `??` → ternary.

Built-in LLT functions: `type(x)`, `length(x)`, `strcat(a, b, …)`, `substr(s, start, len)`.
Additionally dASS registers (access is limited by working-directory permissions):
`fileExists(path)`, `directoryExists(path)`, `readFile(path)`, `listFiles(dir)`, `listDirectories(dir)`.

### Messages (`@messages template`) - for reference only

The syntax exists, but it is **not used** by prompt parts (a part body must render to a string):

```llt
@messages template some_id
{
	@system message { … }
	@user message { … }
	@assistant message { … }
	@foreach name in names { @message { @role 'user' Hello, @name! } }
}
```

### Formatting and indentation

- Leading indentation is normalized by block depth (step `TabSize`, 4 columns by default) - write one indentation
  level per block, the inner relative indentation is preserved.
- Empty/whitespace lines at block boundaries are trimmed.
- Lines consisting only of `@/`, `@* *@`, `@let` and assignments are removed entirely (they leave no blank lines).
- Do not mix tabs and spaces.

## Prompt-part metadata contract

| Key | Required for | Value | Behaviour |
|---|---|---|---|
| `type` | all | `'component'`, `'slot'`, `'skill'`, `'sub-agent'` | must match the manager's type; otherwise the template stays in the library only and does not become a part |
| `guid` | `component`, `slot` | UUID string | part identity + localization key; absent/invalid is **fatal** |
| `strid` | `skill`, `sub-agent` | slug | part identity; absent is **fatal** |
| `title` | - | string | display name; falls back to the template id |
| `description` | `skill`, `sub-agent` | string | **fatal** if empty; this is the model's selection criterion - write it as "when to use" |
| `category` | - | string | grouping in the UI |
| `lang` | - | locale (`'en-US'`, `'iv'`) | without it you get a non-fatal diagnostic, locale becomes invariant |
| `localized_for` | localization files | source locale | links a translation to its original |
| `slot` | `type: 'slot'` | `'system'`, `'persona'`, `'specialization'` | anything else/empty is **fatal** |
| `params_schema` | - | object | parameterization (see below) |

Plus keys recognized by LLTSharp: `lang`, `model`, `model_family`, `version`. Everything else is "additional metadata"
(available for filtering, shown in the UI).

When parts collide (same `guid`/`strid`), the winner is picked first by locale (with a hierarchical fallback to invariant),
then by source priority: **Workdir(4) > Configuration(3) > User(2) > BuiltIn(1)**.
That is, a template from the working directory overrides everything else, and a user file overrides a built-in one.

### Localization

A separate file in `templates/` with the same `guid`/`strid`, `lang: '<target>'` and `localized_for: '<source>'`.
Reference (read-only, do not edit): `src/LLMDesktopAssistant/Prompting/Resources/ru-RU/components.llt` -
there the translation bodies are empty and only the metadata (`title` etc.) is overridden.

## Parameterization (`params_schema` + `@params`)

```llt
params_schema: {
	// text field
	name:    { type: 'textbox/string',  title: 'Name', placeholder: 'Your name', default: 'world', isMultiline: false },
	// dropdown (isEditable: true allows a custom value)
	tone:    { type: 'combobox/string', title: 'Tone', choices: [ 'formal', 'casual' ], default: 'formal' },
	// boolean "combobox" with labels
	shout:   { type: 'combobox/boolean', default: false, trueTitle: 'Yes', falseTitle: 'No' },
	// checkbox
	strict:  { type: 'checkbox', default: true },
	// slider (min/max are mandatory; for integer the default step is 1)
	count:   { type: 'slider/integer', min: 1, max: 10, step: 1, default: 3 },
	temp:    { type: 'slider/number',  min: 0, max: 2, step: 0.1, default: 1.0 },
	// list of homogeneous items
	tags:    { type: 'list', items: { type: 'textbox/string' }, min: 0, max: 5 },
	// nested object
	level1:  { type: 'object', properties: { level2: { type: 'textbox/string' } } }
}
```

Elements: `textbox`, `slider`, `combobox`, `checkbox`, `list`, `object`.
The value type follows the `/`: `string`, `number`, `integer`, `boolean`, `object`, or nothing.
Allowed pairs: `textbox` (string|number), `slider` (integer|number), `combobox` (string|boolean),
`checkbox` (-|boolean), `list` (-), `object` (-).
Common properties of any element: `title`, `description`.
The schema root is a dictionary `key → schema`.

Usage in the body: `@params.name`, `@params.tags[0]`, `@(params ?: 'count' ? params.count : 3)`.

Important: `params` is injected into the context **only if** the part has a `params_schema` (and is removed after
rendering). Values come from settings (per-agent/chat overrides) and are "repaired" by the schema (`CreateOrFixValue`) -
defaults are substituted automatically.

## How a part body is rendered

- Shared context variables come from `IPromptSystemContextExpander` (`Prompting/ContextExpanders`).
- `component` and `slot` render inside the system prompt section; a `slot` is taken by the `(guid, PromptSlotKind)` key.
- `skill` and `sub-agent` render into the skill's/sub-agent's body (by `strid`), i.e. their body is the skill's/sub-agent's prompt.
- Template functions come from `IPromptTemplatePlugin` (file-based + built-in).

## Pitfalls

1. **Literal `@`, `{`, `}` in text.** The most common source of breakage: a prompt that *talks about* markdown
   mentions, JSON, or code with braces. Cure it with escapes (`@@`, `{{`, `}}`) or by wrapping the fragment in five backticks.
2. `@@@name` = a literal `@` + an expression - handy for printing mentions like `@Coder`.
3. `@`-output accepts only simple expressions; `@(a + b)`, `@(x ?? y)`, `@(cond ? a : b)` - always parenthesized.
4. A plain `name` on an absent property throws; for "may be absent" use `?name`, `a?.b`, `??`.
   `?:` ("has property") distinguishes *absent* from *null* - useful for different fallbacks.
5. Do not reuse `guid`/`strid` for different parts and do not change them on existing ones - they are identity
   (and the localization key) in settings.
6. The template `id` (`@template <id>`) is also a public identifier (library search by id). Renaming breaks references.
7. `description` for `skill`/`sub-agent` is not a "human" description but the model's selection trigger:
   phrase it as "Use when …".
8. A broken `params_schema` is not fatal on import, but then there are no parameters → `@params.x` fails at render time.
   Keep the schema and its usage in the body in sync.
9. Do not create `type: 'slider'` - the type is obsolete.
10. Five backticks is the only "string" construct in LLT: three backticks inside a body are safe and often needed for code fences.

## Workflow

1. Clarify the part type (`component` / `slot` / `skill` / `sub-agent`) - the required metadata and the place
   where the part is applied depend on it.
2. Find samples: `fs-glob` over `**/templates/*.llt`, `fs-grep` for `type: '…'`; read 1–2 neighbouring templates
   and match their style.
3. Decide where to write: the user/workdir pack (`templates/<name>.llt`) - not the built-in resources.
4. Assemble the template: `@template <id>` → `@metadata` → body; textual fragments with special characters go into
   a raw block / escapes.
5. If user settings are needed, add `params_schema` and use `@params.*`; verify the keys match.
6. For a translation, use a separate file with the same `guid`/`strid`, `lang` and `localized_for`.
7. Run the checklist below and show the user the finished template/diff.

## Pre-delivery checklist

- [ ] `@template` has an id; the body is a text template.
- [ ] `@metadata` is the first block, all values are constants.
- [ ] `type` is correct and within `component | slot | skill | sub-agent`.
- [ ] `guid` (for component/slot) or `strid` (for skill/sub-agent) is present and unique.
- [ ] `slot` ∈ `system|persona|specialization` for `type: 'slot'`.
- [ ] `description` is filled in for `skill`/`sub-agent`.
- [ ] Literal `@`, `{`, `}` are escaped (`@@`, `{{`, `}}`) or wrapped in a five-backtick raw block.
- [ ] Binary expressions are parenthesized `@(…)`.
- [ ] `params_schema` keys match `@params.*` in the body; slider `min/max` are set.
- [ ] Blocks `{ }` and `@if/@foreach` are balanced; indentation is one level per block.
- [ ] The file lives in `templates/` and has the `.llt` extension.

## Examples

### Component

```llt
@template git_hints
{
	@metadata
	{
		guid: 'fb6c9a5d-8e2b-4f1a-a7b3-e0b1c2d3e4f5',
		lang: 'en-US',
		type: 'component',
		title: 'Git hints'
	}
	You can use Git commands to interact with a version control system.
	Use `execute-shell` for running shell commands, such as:
	```shell
	git status -s
	```
}
```

### Slot with parameters

```llt
@template my_system_slot
{
	@metadata
	{
		guid: '3a4b5c6d-7e8f-9a0b-1c2d-3e4f5a6b7c8d',
		lang: 'en-US',
		type: 'slot',
		slot: 'system',
		title: 'Test parameters',
		params_schema: {
			name: {
				type: 'textbox/string',
				placeholder: 'Your name'
			},
			combo: {
				type: 'combobox/string',
				choices: [ 'Man', 'Woman', 'Combat helicopter' ]
			}
		}
	}
	Alright, this is a system prompt test with parameters!
	Name: @params.name
	Gender: @params.combo
}
```

### Skill (the body is the instructions)

```llt
@template my_skill
{
	@metadata
	{
		strid: 'my-skill',
		lang: 'en-US',
		type: 'skill',
		title: 'My skill',
		description: 'Use when the user asks to audit widget configuration files.'
	}
	# My skill

	1. Find the widget configs.
	2. Check them against the schema.
	3. Change nothing without confirmation.
}
```

### Sub-agent (a one-shot task)

```llt
@template my_sub_agent
{
	@metadata
	{
		strid: 'my-sub-agent',
		lang: 'en-US',
		type: 'sub-agent',
		title: 'My sub-agent',
		description: 'Runs once to summarize a diff and list risky changes.',
		params_schema: {
			target: { type: 'textbox/string', title: 'Target', default: 'HEAD' },
			diff:   { type: 'textbox/string', title: 'Diff', isMultiline: true }
		}
	}
	You analyze a diff and report risky changes.

	Target: @params.target

	Diff:
	`````
	@params.diff
	`````
}
```

## Useful links (read-only)

- Prompt-part samples: `src/LLMDesktopAssistant/Prompting/Resources/components.llt` (components + a slot with `params_schema`).
- Parameter schema: `src/LLMDesktopAssistant/StructuredValues/Parameterization/Parsers/*Parser.cs`.
- Part managers (types and required metadata): `src/LLMDesktopAssistant/Prompting/Management/Prompt*Manager.cs`.
- Render entry points: `src/LLMDesktopAssistant/Prompting/Context/Providers/**` and `**/*setCollector.cs`.
