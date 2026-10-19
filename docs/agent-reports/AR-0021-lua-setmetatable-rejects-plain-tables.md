# AR-0021 - `setmetatable` rejects plain Lua tables, so classic metatable idioms are unavailable

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (`lua-execute` / AsyncLua runtime)
- **First seen:** 2026-10-20

## What I expected

Standard Lua lets any table carry a metatable:

```lua
local mt = { __index = function() return 42 end }
local t = setmetatable({}, mt)
print(t.anything)   -- 42
```

`setmetatable` is the foundation for `__index`, `__newindex`, `__call`, operator overloading (`__add`),
and the object-oriented idiom used by most Lua libraries.

## What happened

`setmetatable({}, mt)` throws:

```
Caught error: AsyncLua.LuaRuntimeException: setmetatable: first argument must have a metatable, got table
Location (at '({}, mt)'):
local obj = setmetatable({}, mt)
                        ^^^^^^^^ line 54, column 25, length 8
```

The message says a table "must have a metatable" to be given one, which reads as a catch-22. It suggests
`setmetatable` is implemented for host objects (C#-backed values that already have a metatable slot), not
for plain Lua tables.

## Why it costs

- OOP in Lua, operator overloading and `__index`-based defaults all fail. An agent writing a widget/view-model
  table with method lookup through `__index` discovers this at runtime, after the design is built.
- The error is clear enough to diagnose, but there is no documented alternative: it is unknown whether
  AsyncLua exposes `rawset`/`getmetatable`/a different API for table metatables.
- Porting any Lua library that uses metatables (class libraries, serializers, proxies) is blocked.

## Repro

```lua
local mt = { __index = function() return 42 end }
local t = setmetatable({}, mt)
print(t.anything)
```

Observed: `setmetatable: first argument must have a metatable, got table`.

## Witnesses

- 2026-10-20 - `lua-execute` syntax sweep - metatable probe in the core-constructs batch; the metatable
  line was the only runtime failure in an otherwise green run.

## What I did instead

Dropped the metatable and wrote explicit functions/fields. For the probe, nothing else was needed; for real
code the workaround is to avoid the pattern entirely.

## Proposed fix

Cheapest first:

1. **Support metatables on plain tables** in the runtime, so `setmetatable` follows Lua semantics.
2. If plain-table metatables are intentionally unsupported, **document it** next to `lua-execute` and in the
   `lua-script-authoring` skill: "metatables are only available on host objects; do not use `__index`/`__add`
   on Lua tables".
3. If there is an alternative API (`dass`-level object construction, `dass.ui` view models), name it in the
   error message instead of "first argument must have a metatable".

## Unverified

- Whether `getmetatable` works on a plain table (not tested).
- Whether host objects (e.g. the widget/view-model types from `dass.ui.create_control`) accept
  `setmetatable`; only a plain `{}` was tried.
- Whether the restriction is AsyncLua-wide or specific to the dASS embedding; the runtime is a sibling
  repository and was not traced.
