# Lua API exposure

Status: resolved
Type: grilling
Blocked by: 01

## Question

Design the **Lua API** surface for commands (e.g. `dass.commands.*`):

- listing the commands available to the current chat;
- invoking a command (with what arguments, and from which context);
- whether scripts can observe the resulting message / fingerprint.

Note: RPC is not implemented, and the engine must be callable **without a UI** (the Lua API is the non-UI frontend for v1).

## Answer

### Namespace & functions

- `LuaApiCommands : LuaApiBase`, `Namespace => "dass.commands"`, `[LuaApi(chatScoped: true)]`, with `Manuals` (so `dass.help()` prints them).
- `dass.commands.list()` — table of the commands available to the chat: `name`, `namespaces`, `description`, `source`, `argument_schema` (mirrors `dass.tool.list()`).
- `await dass.commands.invoke(token, arguments?, generate?, wait_for_generation?)`.
- **Parameter names are `snake_case`.**

### Invoke semantics

- Routed through **`IChatMessageInsertionService.TryInsertUserInputAsync`** (ticket 05), so a Lua-invoked command behaves exactly like a user one: it creates a message, validates, and **blocks on failure**.
- The inserted message is attributed to the login **`"Script"`**. (A broader chat-oriented Lua API is coming later and is only lightly related to commands — see the map's Out of scope.)
- `arguments` may be:
  - a **string** → used verbatim as the raw argument text;
  - a **table** → the structured form. Lua tables are array *and* hash at once, e.g. `{ "arg1", key1 = "value1" }`; the API serialises it back into the argument text (array part → positional tokens, hash part → `key=value`), honouring quoting.
- `generate` (`bool`, default `false`) → the `GenerateIntent`.
- `wait_for_generation` (`bool`, default `true`) → whether to await the chat generation when it happens. The command's own execution is always awaited.

### Return

- A table `{ success, error, message_index, fingerprint }`, where `fingerprint` is the flat snapshot from ticket 06. This pairs well with the future chat API.

## Comments

- Single round. The login `"Script"` and the future chat Lua API were flagged as adjacent but not part of this effort.
