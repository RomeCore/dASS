# AR-0020 - `lua-execute` renders a returned table as `(table: <ptr>)` instead of a readable result

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `lua-execute` (return-value rendering)
- **First seen:** 2026-10-20

## What I expected

The tool description says:

> Executes Lua and returns the script result along with messages printed by `print` function

Returning a table is the natural way for a Lua script to hand structured data back:

```lua
return { ok = true, count = 3, items = { "a", "b" } }
```

I expected the tool result to contain that data in some readable form (JSON, a Lua table dump, or the
structured-result channel).

## What happened

The transcript shows only the pointer:

```
Script returned: (table: 1C453C9)
```

Scalars are rendered as-is (`Script returned: (42)`, `Script returned: (plain string result)`), but tables
are not serialised at all. `dass.tool.result.set_structured({ ... })` is accepted without error, but the
text channel still shows nothing about the table.

## Why it costs

- A returned table is silently discarded from the model's view. The script "succeeds" and the data is gone;
  the agent must add a `print(json.encode(t))` round or switch to `set_structured` and hope the UI channel
  carries it.
- This is the silent class in miniature: the call reports success, the result is empty, and nothing warns.
- It pushes every Lua author toward `print(json.encode(...))` as the only reliable return path, which
  duplicates the result: once as prose, once as a dead pointer.

## Repro

```lua
return { answer = 42, list = { 1, 2, 3 } }
```

Observed:

```
Script returned: (table: 1C453C9)
```

Contrast:

```lua
return 42
```

```
Script returned: (42)
```

## Witnesses

- 2026-10-20 - `lua-execute` feature test - basic API run; the table return was the first script of the
  session and produced only a pointer.

## What I did instead

Returned a string and printed the structured payload separately:

```lua
print(json.encode(result))
dass.tool.result.set_structured(result)
return 'done'
```

## Proposed fix

Cheapest first:

1. **Serialise returned tables** in the text result (JSON-like, with cycle detection and a depth/width cap).
   A Lua table with string/number keys maps cleanly onto the existing structured-result machinery.
2. If serialisation is deliberately out of scope, **say so in the description**: "return a string; tables
   are rendered as a pointer and are not serialised".
3. Mention `dass.tool.result.set_structured` in the tool description next to the return-value paragraph,
   since it is currently only discoverable from `manuals(dass.tool.result)`.

## Unverified

- Whether the UI renders `set_structured` as a separate block. I cannot see the UI from the tool result; the
  text channel is what was observed, and it showed nothing. The user was asked but has not reported back.
- Whether a table return is intentionally treated as "opaque handle" for widgets (`dass.ui.create_control`
  returns something table-like), in which case the pointer may be deliberate for that path.
- Whether arrays (`return { 1, 2, 3 }`) behave differently from maps - only a mixed table was returned.
