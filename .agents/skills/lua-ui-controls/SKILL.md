---
name: lua-ui-controls
description: Author or debug a dASS Lua tool that renders its own Avalonia UI - the AXAML string plus dynamic view model packed by `dass.ui.create_control` and attached with `dass.tool.result.append_data`. Use when a Lua tool needs a custom interface, when a dynamic control renders nothing or fails to update, when a control's buttons stop working after a chat reload, or when the user asks how Lua-driven UI in dASS is shaped.
---

# Lua UI controls (dASS)

A Lua tool renders its own interface inside the chat by packing an AXAML string plus a view model and appending it to the tool result. Two calls carry the feature:

```lua
local ui = dass.ui.create_control(xaml, vm)   -- pack AXAML + view model into a handle
dass.tool.result.append_data(ui)              -- render it under this tool call
```

`create_control` builds no control and validates no markup - it packs a value. The rendering happens later, in the chat, inside a `DynamicAxamlControl`. That delay is both the point and the hazard: nothing raises at the two calls above, so a script whose markup binds the wrong keys - or whose handle was never appended from a running tool - renders an empty box in silence. Every run of this skill ends with three answers on screen: what the AXAML binds, how the script mutates it, and what survives a reload.

## Steps

### 1. Pack the handle

`dass.ui.create_control(xaml, view_model) -> uicontrol`

- `xaml` - a non-empty string, loaded at runtime, so it uses reflection bindings: `{Binding key}` and `{Binding [key]}`. Keep compiled bindings and `x:DataType` out of the string.
- `view_model` - a Lua table, or `nil`. A table becomes the dynamic view model (step 3); `nil` gives an empty one for purely static markup.

The result is a handle: a plain value, no methods, reusable. The same handle may be appended any number of times.

*Done when:* the call returns a handle and every key the markup binds exists on the table.

### 2. Append it from a tool

`dass.tool.result.append_data(...)`

One UI element is appended per argument, in call order. Outside a running tool the call is a no-op; on anything that is not a `uicontrol` it raises. The append belongs in the tool body - a script that packs a handle and is never run as a tool shows nothing, silently.

*Done when:* the append runs inside the tool body, or the author has named the no-op as the reason nothing renders.

### 3. Drive the view model from Lua

The table **is** the view model. Its shape decides the bindings:

| Lua member | Binding |
|---|---|
| string key → scalar | `{Binding key}` |
| string key → table | `{Binding key}` (a nested view model) |
| string key → function | `{Binding key}` as a command |

The view model injects two helpers into the table:

- `vm:set(name, value)` - write the member *and* raise the change;
- `vm:notify(name)` - raise the change after writing the table directly.

The optional `__changed(self, name, old, new)` runs whenever the view model itself writes.

Commands use method semantics: declare `function(self)` or `function(self, param)`. A command that updates a member calls `self:set(name, value)`.

*Done when:* every member the markup binds is written through `set`/`notify` - a plain `vm.key = ...` updates nothing until `notify` follows.

### 4. Expect the reload

The tool call persists the UI: the AXAML string and a BSON **snapshot** of the view model travel with it, and on reload the view model is rebuilt from that snapshot. Two consequences follow:

- **Lua functions are dropped.** Commands go dead after a reload: the markup still renders, but its buttons do nothing. A control that must stay live is driven from persisted state, not from a closure.
- **Only top-level members are snapshotted.** Nested mutation reaches the snapshot at the next top-level write.

*Done when:* the author has accepted dead-after-reload commands, or routed the control's live behaviour through persisted state instead of a closure.

### 5. Verify in a real tool call

`lua-execute` is itself a tool, so both APIs work inside it and the control renders under that call. Paste the markup and the view model, append the handle, watch the rendered result, then change a member with `set` and confirm the UI follows.

*Done when:* the control rendered in a real tool call and a scripted `set` changed what the markup shows.

## Reference

### The dynamic view model, precisely

- Backed by the Lua table itself (`LuaValueDynamicViewModel`).
- Members project live: scalars as raw values, nested tables as cached child view models, functions as asynchronous commands.
- Service keys `set`, `notify`, `__changed` are hidden from bindings, so they cannot collide with data members.
- Both `{Binding key}` and `{Binding [key]}` resolve, and both refresh on `set`/`notify`.
- `nil` view model → an empty `DictionaryDynamicViewModel`: it renders, with no Lua-side members.

### What the markup may contain

Any Avalonia markup, with any root `Control` (not only panels). Binding is reflection-based and flows both ways - a `TwoWay` binding writes back through the same view model. The fragment has no code-behind and no event handlers; interaction goes through commands. It runs with full trust and may reference any type the loader resolves.

### What persists, and how

The additional data stores its AXAML string plus a `BsonValue` snapshot of the view model. The snapshot is built from the Lua table through the structured-values pipeline (`LuaStructuredConverter` → `BsonStructuredConverter`) in tolerant mode: functions, userdata and other unsupported values are dropped, while containers keep the entries that convert. On load the snapshot becomes a `NodeValueDynamicViewModel`.

### Where the code lives

| Concern | File |
|---|---|
| `dass.ui.create_control` | `Scripting/Lua/API/LuaApiUi.cs` |
| The `uicontrol` handle | `Scripting/Lua/LuaUiControl.cs` |
| `append_data` | `Scripting/Lua/API/LuaApiToolResult.cs` |
| The renderer | `Controls/DynamicAxamlControl.cs` |
| The additional data + view | `LLM/MVVM/Additional/DynamicAxamlControlAdditionalData.cs` (+ `.axaml`) |
| Tolerant Lua → structured conversion | `StructuredValues/Converters/LuaStructuredConverter.cs` |

### What bites

- **Markup whose bindings miss the table** renders empty - nothing validates the string, so nothing warns.
- **Markup written for compiled bindings** (`x:DataType`, `{CompiledBinding}`) fails at render time, not at `create_control` time.
- **`append_data` from a script that is not a running tool** is a silent no-op.
- **A control that mutates members with `vm.key = ...`** stays frozen until `vm:notify(key)` runs.
- **Buttons that call closures** work while the tool call is live and go dead after a reload.
