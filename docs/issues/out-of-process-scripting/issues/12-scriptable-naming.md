# `meta` becomes `scriptable` in the scripting surface

Status: done
Type: task
Blocked by: 08

## Question

The metatool subsystem died when scriptable tools became addons, but the word stayed in three places: the Python
engine's settings, the environment folder, and the tool's provenance enum. With four scriptable kinds in the family
a user reads "meta" and has no way to learn what it means.

The name has to go wherever it means "a script that creates a tool/command/context", and stay wherever it means
something else.

## Answer

### Renamed

| Was | Becomes |
|---|---|
| `ToolSource.Meta` | `ToolSource.Scriptable` |
| `ToolBehaviour.Meta` | `ToolBehaviour.Scriptable` |
| `ToolBehaviourCategory.Meta` | `ToolBehaviourCategory.Scriptable` |
| `tool.source.meta` | `tool.source.scriptable` ("Scriptable") |
| `tool.behaviour.meta` / `.hint` | `tool.behaviour.scriptable` / `.hint` |
| `tool.behaviour.category.meta` | `tool.behaviour.category.scriptable` |
| catalogue token `source: meta` | `source: scriptable` (`ToolAgenticSearchProvider`) |
| Lua `tool.source` value `meta` | `scriptable` (`LuaApiTools`) |
| `PythonEnvironmentConfiguration.PythonVenvActivateScriptPath` | `.VenvActivateScriptPath` |
| `PythonEnvironmentConfiguration.PythonMetaVenvActivateScriptPath` | `.AddonVenvActivateScriptPath` |
| `PythonHelperService.CreateLaunchParameters(..., bool isMetaTool)` | `bool useAddonEnvironment` |
| `env.python.hint`, `python.meta_venv_hint`, `python.meta_venv_script`, `python.select_meta_venv_script` | rewording plus `python.addon_venv_*` |
| `Directories.Metatools` | deleted - created on startup, read by nobody |

The two Python venvs stay two: one for the native `execute-python` tools, one for scriptable addons, the second
falling back to the first. What dies is the word, not the split.

### Kept

- The R.I.P. epitaph at the top of `ScriptableToolParser.cs` - it is a tombstone with dates, not a term. It is the
  only place where the word is meant to survive.
- `docs/help/tools/metatools.md` - ticket 09 rewrites the help pages for four languages anyway; moving the file now
  would mean moving it twice.

### Accepted cost

Serialized values move with the rename: an agent settings file stores behaviour flags as strings, so a stored
`Meta` stops matching, and the renamed Python venv properties lose their JSON keys, silently dropping a configured
venv. With no users yet both are free; recording it so the loss is a decision rather than a surprise.

## Comments

- Split out of the 2026-10-25 grilling. Implemented as its own commit (`cc67ba87`), before the bash engine, so the
  rename does not ride along in an engine diff.
- `ToolBehaviourCategory.Meta` was renamed as well: with it gone the only surviving "meta" is the R.I.P. epitaph,
  which is a tombstone with dates rather than a term.
- The two venvs stayed two - one for the native `execute-python` tools, one for scriptable addons with a fallback -
  and only the names changed, plus the hint text now says which is which.
