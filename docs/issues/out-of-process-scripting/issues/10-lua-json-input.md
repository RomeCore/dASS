# Lua honours `json-input`

Status: done
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

All three sites now ask the addon's `InputMode`:

| Site | `auto` / `false` | `true` |
|---|---|---|
| `LuaToolEngine` | `StructuredLuaConverter.JsonNodeToLuaValue(args)` | `new LuaString(args?.ToJsonString() ?? "null")` |
| `LuaScriptCommandExecutor` | `LuaStructuredConverter.ToLuaValue(CommandArgsNode.Create(...))` | `new LuaString(commandArgs.ToJsonNode()?.ToJsonString() ?? "null")` |
| `LuaScriptContextProviderBase` | `new LuaString(previous)` | `new LuaString(JsonValue.Create(previous)!.ToJsonString())` |

Three inline branches, no helper: the rule is one `is ScriptInputMode.Json` test and the conversion is a
constructor, so anything shared would be an indirection with three call sites and no content.

A context's variable is text, so `json-input: true` there means "the JSON form of that text" (`"…"` quotes and all) -
useless in practice but honest, and the alternative would be a flag that silently does nothing on one kind.
**An absent `previous` stays `LuaNil` in both modes**: "there is no previous text" is not a JSON value, and a body
that tests `previous == nil` has to keep working.

Nothing changes at load time: the Lua descriptors already declare `StructuredInput`, so `json-input: false` was
legal and already meant "give me the structure". `LuaScriptInput` naming was rejected on the way - three one-line
branches do not need a class.

## Comments

- Landed 2026-10-26. Verified by `tests/LLMDesktopAssistant.Tests/Scripting/LuaJsonInputTests.cs` (4 cases): the
  `type(tool_args)` of each mode (`string`, `table`, `table`) and that the JSON text really is the JSON of the
  arguments (`{"text":"hi"}` printed verbatim). The harness is `ToolTestContext` plus a stub
  `ILuaScriptsetStateWatcher`, since `LuaService` takes exactly one dependency.
- Trap found while writing the test: **`json` is not part of a bare Lua state.** A chat has it (an addon or the API
  surface provides it), a freshly constructed `LuaService` does not, so a body that calls `json.decode` cannot be
  unit-tested and an addon should not assume the namespace without checking `type(json) == "table"`.
- `lua-echo.lua` in the scratch pack is the manual probe: it prints `type(tool_args)` and the value, which shows
  `string` on a `json-input: true` addon and the JSON text itself.
