---
name: addon-parameterization
description: Declares user-editable parameters for a dASS addon and reads them at run time - params_schema in LLT prompt parts, params-schema in scriptable addons (tools, slash commands, prompt contexts, hooks, Lua scripts), consumed as @params.* in a template or the `params` variable in a script. Use when an addon needs settings in its card, when a script sees nil/undefined params, or when the user asks how parameterization works.
allowed-tools: [fs-read_entry, fs-grep, fs-glob, fs-edit, fs-write_file, fs-create_directory]
---

# Addon parameterization (dASS)

One schema, two spellings by context:

| Addon body | Frontmatter key | The value arrives as |
|---|---|---|
| LLT prompt part (`templates/*.llt`) | `params_schema` in `@metadata` | `@params.<key>` in the template |
| Scriptable addon (`tools/`, `commands/`, `context/`, `hooks/`, `scripts/lua/`) | `params-schema` | the `params` global/variable of the script |

The addon frontmatter reader normalizes `-`, `_` and case, so `params-schema` and `params_schema` both resolve. **Canonical is `params-schema`**; write that, and do not invent aliases in docs.

## The schema

The root is a dictionary `key → element`. Elements are `textbox`, `slider`, `combobox`, `checkbox`, `list`, `object`.

```yaml
params-schema:
  name:   { type: 'textbox/string', title: 'Name', placeholder: 'Your name', default: 'world' }
  tone:   { type: 'combobox/string', title: 'Tone', choices: [ 'formal', 'casual' ], default: 'formal' }
  shout:  { type: 'combobox/boolean', trueTitle: 'Yes', falseTitle: 'No', default: false }
  strict: { type: 'checkbox', default: true }
  count:  { type: 'slider/integer', min: 1, max: 10, step: 1, default: 3 }
  temp:   { type: 'slider/number', min: 0, max: 2, step: 0.1, default: 1.0 }
  tags:   { type: 'list', items: { type: 'textbox/string' }, min: 0, max: 5 }
  nested: { type: 'object', properties: { level2: { type: 'textbox/string' } } }
```

The value type follows the `/`: `string`, `number`, `integer`, `boolean`, `object`, or nothing.

| Element | Allowed value types |
|---|---|
| `textbox` | `string`, `number` |
| `slider` | `integer`, `number` |
| `combobox` | `string`, `boolean` |
| `checkbox` | none, `boolean` |
| `list` | none |
| `object` | none |

Any element accepts `title` and `description`. A slider needs `min` and `max`; the integer step defaults to `1`. A list needs `items`; an object needs `properties`. `combobox/string` may set `isEditable: true`, `combobox/boolean` - `trueTitle`/`falseTitle`.

## Where the values live

- **Scriptable addon** - the card grows a collapsible Parameters block as soon as the schema is present. Values are the per-chat (and per-agent) change entry of the addon, saved like every other override.
- **LLT prompt part** - values are the part selection's settings; the same schema drives the card editor.
- The editor is materialized lazily, on the first expansion of the block: an addon the user never opened stores nothing.

At every run the resolver applies `CreateOrFixValue`: missing keys are created, wrong types are repaired, defaults are substituted, and the fixed value is written back. A script therefore sees a complete, typed value even when the user changed nothing.

## Reading `params`

**LLT body:** `@params.name`, `@params.tags[0]`, `@(params ?: 'count' ? params.count : 3)`. The `params` object exists only when the part declares `params_schema`, and is removed after rendering.

**Lua** (tool, command, context, hook, and the `scripts/lua` load chunk): `params` is a table - `params.name`, `params.tags[1]`. With `json-input: true` it is a JSON **string** the body decodes itself.

**Python / PowerShell:** `params` is a native structure - a dict / `PSCustomObject`. With `json-input: true` Python gets a JSON string too.

**Bash / Batch:** `params` is always JSON text; decode it in the body.

The addon's own arguments stay separate: `tool_args` / `command_args` carry the invocation, `params` carries the user's settings. Never merge them.

## What bites

- **No schema, no `params`.** The variable is absent, not an empty table: `params.name` fails with "attempt to index nil". Guard with `params and params.name`, or declare the schema.
- **A broken schema is not fatal.** The addon still loads with an `InvalidParameterSchema` diagnostic and no parameters; `@params.x` then fails at render time and Lua sees `nil`. Fix the schema, do not guard forever.
- **Renaming a key orphans its value.** Stored values are keyed by the schema key; a rename is a new parameter with the default.
- **Only the kinds whose body consumes parameters read `params-schema`.** It is a scriptable-addon field: a plain `SKILL.md` / `AGENT.md` ignores it. A template skill or sub-agent declares `params_schema` in its LLT metadata instead.
- **`scripts/lua` gets `params` at load only.** The body runs once per chat and on reload; a namespace function called later finds no `params` of its own.
- **Values are embedded in an out-of-process script's source.** Bash, PowerShell and Batch receive them as quoted literals, so an enormous value is an enormous script line (Batch refuses a line over 8191 characters).
- **The root name is fixed.** `params` is conventional and already wired; renaming the variable in the body does nothing.

## Checklist

- [ ] The key is canonical: `params-schema` in addon frontmatter, `params_schema` in LLT `@metadata`.
- [ ] Every key the body reads is declared.
- [ ] Sliders have `min`/`max`; lists have `items`; objects have `properties`.
- [ ] The body guards an absent `params` (or the schema is guaranteed).
- [ ] Defaults make the addon useful before the user opens the card.

## Neighbours

- LLT prompt parts and their metadata: the `llt-prompt-parts` skill.
- Scriptable tools: `tool-authoring`. Slash commands: `command-authoring`. Prompt contexts: `context-authoring`. Hooks: `hook-authoring`. Shared Lua libraries: `lua-script-authoring`.
- The schema implementation: `src/LLMDesktopAssistant/StructuredValues/Parameterization/`.
- Value resolution for scripts: `src/LLMDesktopAssistant/Scripting/ScriptAddonParameters.cs`.

---

**Note**: all paths in the skill are relative to skill's home path: *E:\CSharp Projects\LLMDesktopAssistant\.agents\skills\addon-parameterization*
