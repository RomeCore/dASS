---
name: lua-ui-controls
description: Author or debug a dASS Lua tool that renders its own Avalonia UI - the AXAML string plus dynamic view model packed by `dass.ui.create_control` and attached with `dass.tool.result.append_data`. Use when a Lua tool needs a custom interface, when it must pause for the user to fill that interface (awaiting a `task.create()` handle), when a control renders nothing or fails to update, when an `ItemsControl` or list stays empty, when its buttons stop working after a chat reload, or when the user asks how Lua-driven UI in dASS is shaped.
---

# Lua UI controls (dASS)

A Lua tool renders its own interface inside the chat by packing an AXAML string plus a view model and appending it to the tool result. Two calls carry the feature:

```lua
local ui = dass.ui.create_control(xaml, vm)   -- pack AXAML + view model into a handle
dass.tool.result.append_data(ui)              -- render it under this tool call
```

`create_control` builds no control and validates no markup - it packs a value. The rendering happens later, in the chat, inside a `DynamicAxamlControl`. That delay is both the point and the hazard: nothing raises at the two calls above, so a script whose markup binds the wrong keys - or whose handle was never appended from a running tool - renders an empty box in silence. Every run of this skill ends with four answers on screen: what the AXAML binds, how the script mutates it, how the tool gets the user's answer back, and what survives a reload.

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
| string key → table (dict) | `{Binding key}` (a nested view model) |
| string key → table (array) | `{Binding key}` as `ItemsControl.ItemsSource` (see *Lists and `ItemsControl`*) |
| string key → function | `{Binding key}` as a command |

The view model injects two helpers into the table:

- `vm:set(name, value)` - write the member *and* raise the change;
- `vm:notify(name)` - raise the change after writing the table directly.

Two channels carry state, and they are not interchangeable:

- **Commands** - a control invokes Lua. Method semantics: `vm.submit = function(self, param) ... end`, updating through `self:set(...)`.
- **Two-way bindings** - a control writes straight into a member: `{Binding key, Mode=TwoWay}`. `TextBox.Text` and `CheckBox.IsChecked` need no command; read `self.key` inside a later command to pick the value up.

The optional `__changed(self, name, old, new)` fires on **every** view-model write from **either** side - a `vm:set(...)` and a UI write-back both reach it. A member you set from inside it re-enters the handler, so guard that with a flag.

Declare commands with the expression form `vm.submit = function(self) ... end` or the colon form `function vm:submit() ... end`. The dotted statement form `function vm.submit(self) ... end` is a **parser crash** - see *What bites*.

*Done when:* every member the markup binds is written through `set`/`notify`, and every command is declared in a form the parser accepts.

### Lists and `ItemsControl`

A member whose value is a Lua **array** renders as a list:

```xml
<ItemsControl ItemsSource="{Binding items}">
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding label}"/>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

- The nested view model behind an array member **is** a list, so `{Binding items}` is a valid `ItemsSource` as-is.
- `.Items` still resolves and returns the raw array view (the array part as a list object) - use it only when you specifically want that view; both forms are live and binding-equivalent.
- Items are nested view models: inside the `DataTemplate`, bind their members the same way as the top-level table (`{Binding label}`, `{Binding [label]}`, commands).
- The member must actually be an **array**. A dictionary member bound to `ItemsSource` renders an empty list, with no error.
- Refresh by replacing the whole member (`vm:set("items", newArray)`); the control re-points at the new list.

*Done when:* a nested array member is bound as `ItemsSource`, its `DataTemplate` member names match the item keys, and adding an item to the array shows up in the control.

### 4. Await the user

An interactive control does not have to leave the tool running blind: the tool can park until the user acts, then resume with their answer.

```lua
local t = task.create()                                -- completion handle (metatable: set_result / set_error)
local resolved = false
local function resolve(v) if not resolved then resolved = true; t:set_result(v) end end

vm.submit = function(self) resolve({ name = self.name, agree = self.agree }) end
vm.cancel = function(self) resolve(nil) end

dass.tool.result.append_data(dass.ui.create_control(xaml, vm))
time.set_timeout(600000, function() resolve(nil) end)  -- safety net: the tool can never hang forever

local result = await t                                 -- suspends the script; the UI stays live and clickable
if result then ... use result ... end
```

The control renders and stays interactive **while the script is parked on `await`** (verified: a live `set_interval` ticker and a button click both fired mid-await). The command resolves the handle from its closure. No command is needed for *input* - a TwoWay field is already in `vm` by the time `submit` runs.

*Done when:* the tool resumes exactly once - on the user's action or the safety timeout - and the resumed branch reports what it got.

### 5. Expect the reload

The tool call persists the UI: the AXAML string and a BSON **snapshot** of the view model travel with it, and on reload the view model is rebuilt from that snapshot. Consequences:

- **Data survives, closures do not.** Scalars and containers come back; functions - commands and `__changed` - are dropped, so command-bound buttons render disabled. An interactive control persists as *state*, never as behaviour.
- **Only top-level members are snapshotted.** Nested mutation reaches the snapshot at the next top-level write.
- **Re-arm by re-running the tool.** Re-running the same `lua-execute` re-injects the closures under the persisted layout.

*Done when:* the author has accepted dead-after-reload commands, or routed the control's live behaviour through persisted state / a re-run.

### 6. Verify in a real tool call

`lua-execute` is itself a tool, so both APIs work inside it and the control renders under that call. Paste the markup and the view model, append the handle, watch the rendered result, change a member with `set` and confirm the UI follows; if the control awaits, click through it and confirm the script resumed.

*Done when:* the control rendered in a real tool call, a scripted `set` changed what the markup shows, and any `await` was resumed by a real click.

## Reference

### The dynamic view model, precisely

- Backed by the Lua table itself (`LuaValueDynamicViewModel`).
- Members project live: scalars as raw values, nested tables as cached child view models, functions as asynchronous commands.
- Service keys `set`, `notify`, `__changed` are hidden from bindings, so they cannot collide with data members.
- Both `{Binding key}` and `{Binding [key]}` resolve, and both refresh on `set`/`notify`.
- `__changed` is not Lua-only: a UI write-back into a member fires it too (verified - a `TextBox` edit produced the exact character count Lua reported back).
- `nil` view model → an empty `DictionaryDynamicViewModel`: it renders, with no Lua-side members.

### Arrays, lists and items, precisely

- `Items` on a structured view model is a `DynamicArrayView`: a read-only `IReadOnlyList<object?>` over the array part, with items materialized on demand and cached per index.
- The structured view model **is itself the list** over its array part, so a container member can be bound as `ItemsSource` directly; `.Items` returns that same array part as an explicit view object.
- Array length comes from the source: the Lua `#` (array part) for `LuaValueDynamicViewModel`, `INodeArrayValue.Items.Count` for `NodeValueDynamicViewModel`. Only the contiguous `1..n` array part counts.
- Writing or notifying a member of the owning view model raises a collection reset, so a bound list refreshes from the live data.
- A view model whose root is an array binds as the list itself (`ItemsSource="{Binding}"`).
- `Count` is dual-purpose on a structured view model: the public `Count` is the number of dictionary entries, while the list length is the array part's length.

### When a list renders empty

Rendering errors are silent, so walk the list from the outside in:

- The bound member is actually an **array** (elements under `#`), not a dictionary.
- The `ItemsControl.ItemTemplate` member names match the item keys exactly (same case).
- The append ran inside a running tool (`dass.tool.result.available()`).
- The script did not error before the append - on error `print` is dropped, so write a marker to a file with `fs.write` and read it back.
- A scripted change did not appear: it went through `set`/`notify` (a plain `vm.key = ...` stays frozen).

### Awaiting input, precisely

- `task.create()` returns a handle whose metatable exposes `set_result(value)` and `set_error(err)`. There is **no manual entry** - reach it by introspection (`getmetatable(t)`).
- `await t` suspends the script until the handle resolves and yields the value (verified).
- The suspension parks the script, **not the UI thread**: the control keeps rendering and responding while the tool awaits.
- Resolution is first-wins: a second `set_result` on the same handle raises.
- **No safety net means no bound** - with nothing resolving the handle the tool waits on the user (or the execution cancel). A `time.set_timeout` that resolves it is the backstop.

### What the markup may contain

Any Avalonia markup, with any root `Control` (not only panels). Binding is reflection-based and flows both ways - a `TwoWay` binding writes back through the same view model. The fragment has no code-behind and no event handlers; a control either binds a member or invokes a command. It runs with full trust and may reference any type the loader resolves.

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
| The array/list surface (a container member as `ItemsSource`) | `MVVM/Dynamic/StructuredDynamicViewModel.cs` (`Items`, list projection) |
| The read-only array view | `MVVM/Dynamic/DynamicArrayView.cs` |
| Dynamic member reflection (how `{Binding key}` resolves) | `MVVM/Dynamic/DynamicViewModelTypeInfo.cs` |
| `task.create()` (the await handle) | `AsyncLua` repo → `src/AsyncLua/Libraries/TaskLibrary.cs` (not in dASS) |

### What bites

- **A dotted function *statement*** - `function vm.bump(self) ... end` - crashes the AsyncLua parser with a bare `NullReferenceException` (`AsyncLuaParser.DeclareStatements`) before a line runs; the message names the host, not the construct. Use `vm.bump = function(self) ... end` or `function vm:bump() ... end`. Traced and reported as `docs/agent-reports/AR-0005-*`.
- **A bind that misses the table** renders empty - nothing validates the string, so nothing warns.
- **Markup written for compiled bindings** (`x:DataType`, `{CompiledBinding}`) fails at render time, not at `create_control` time.
- **`append_data` from a script that is not a running tool** is a silent no-op.
- **A member written with plain `vm.key = ...`** stays frozen until `vm:notify(key)` runs.
- **`set_result` called twice** raises - resolve the handle behind an idempotent guard.
- **A member set from inside `__changed`** re-enters the handler - guard it with a flag.
- **`await` with no safety timeout** waits forever if the user never acts.
- **`print` output is discarded when the script errors**, so a failing UI script cannot be traced with `print` - write the trace to a file with `fs.write` and read it back.
- **Leveled long strings (`[==[ ... ]==]`) are not supported** by the AsyncLua parser - it fails at the opening `[==[`. Embed the AXAML in a plain `[[ ... ]]` string and keep `]]` out of the markup (concatenate pieces if the markup contains one).
- **A list member that is actually a dictionary** renders an empty `ItemsControl` with no error - confirm the member is an array (it has elements under `#`).
