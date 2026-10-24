# Lua honours `json-input`

Status: ready-for-agent
Type: task
Blocked by: 08

## Question

`json-input` is parsed, validated against `ScriptEngineCapabilities.StructuredInput` and carried on the addon —
but no Lua path reads it. All three Lua engines always pass a native structure:

- `LuaToolEngine`: `g["tool_args"] = StructuredLuaConverter.JsonNodeToLuaValue(args)` (a table);
- `LuaScriptCommandExecutor`: `globals["command_args"] = LuaStructuredConverter.ToLuaValue(...)` (a table);
- `LuaScriptContextProviderBase`: `globals["previous"] = LuaValueConverter.ToLuaValue(previous)` (a string).

Make the flag mean what the frontmatter says: `json-input: true` hands the variable over as a **JSON string**
(the body calls `json.decode`), `json-input: false` and `auto` keep the native table. That is the same rule the
out-of-process engines already follow, and without it `json-input` on a `.lua` addon is a silent no-op — the exact
failure mode the family was built to remove.

## Answer

## Comments

- Split out of the "kind adapters and flags" ticket on 2026-10-25: the Lua path is not in focus there, and touching
  three engines mid-slice would have mixed two concerns in one diff.
