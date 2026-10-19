# AR-0009 - A function call as the last element of a table constructor expands into extra values (parentheses do not stop it)

- **Status:** fixed
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (Lua / `lua-execute` and scriptable commands - AsyncLua table-constructor evaluation)
- **First seen:** 2026-10-12

## What I expected

In Lua 5.5 a function call in the **last** field of a table constructor expands to all of its return values; a call
anywhere else is truncated to one value. `string.format` returns exactly one value, and a **parenthesized** expression
`(f())` is *always* a single value, even in the last field. So both of these must build a two-element table:

```lua
local t1 = { "h", string.format("%s %s", "a", "b") }        -- expected 2
local t2 = { "h", (string.format("%s %s", "a", "b")) }      -- expected 2 (parens truncate)
```

## What happened

The table gained extra elements. Measured in one `lua-execute` run:

| Expression | `#`  | Expected |
|---|---|---|
| `{ "h", string.format("%s %s", "a", "b") }` | 2 | 2 |
| `{ "h", string.format("%s %s", tostring(1), "b") }` | **4** | 2 |
| `{ "h", (string.format("%s %s", tostring(1), "b")) }` | **4** | 2 (parentheses must truncate) |
| `local v = string.format("%s %s", tostring(1), "b"); { "h", v }` | 2 | 2 |

The extra values look like the call's **own format template plus its arguments** leaking into the result. The trigger
correlates with a **nested call** among the arguments of the trailing call (`tostring(1)` above): a call whose args are
only literals (`"a", "b"`, or even the non-string literal `12.5`) did **not** expand.

Seen in the wild: the showcase command `/mark` built its body as

```lua
local lines = {
    string.format("**%s**", title),
    string.format("level=%s · weight=%s · pin=%s · tag=%s", level, tostring(weight), tostring(pin), tag or "-"),
}
```

and the message part came out as the correctly formatted line **followed by the raw template and each argument on its
own line**:

```
**Deploy**
level=error · weight=999.999 · pin=false · tag=release
level=%s · weight=%s · pin=%s · tag=%s
error
999.999
false
```

(the `tostring(...)` calls in the second `string.format` are the nested calls that trip it).

**Update 2026-10-20 (AsyncLua 0.7.0):** all four repro rows now report 2, including the nested-argument
and parenthesised cases. The constructor no longer expands the trailing call.

## Why it costs

- Any Lua addon that builds a string/array with the idiomatic `{ ..., fmt(...) }` silently acquires junk elements. In a
  rendered message part it is visible; wired into an argument list or a `table.concat` it would be **silent and
  plausible-looking**, which is the worse case.
- One debugging round per occurrence: I burned five `lua-execute` probes (bisecting argument types, nesting, and
  parentheses) before the shape emerged.
- It poisons trust in `lua-execute` for string building, the same way AR-0006 did for patterns.

## Repro

```lua
print(#{"h", string.format("%s %s", "a", "b")})            -- 2  (correct)
print(#{"h", string.format("%s %s", tostring(1), "b")})    -- 4  (should be 2)
print(#{"h", (string.format("%s %s", tostring(1), "b"))})  -- 4  (parentheses must give 2)
local v = string.format("%s %s", tostring(1), "b")
print(#{"h", v})                                            -- 2  (correct)
```

## Witnesses

- 2026-10-12 - first scriptable-command session (`/mark` showcase command) - `lines = { header, string.format("level=%s ...", level, tostring(weight), tostring(pin), tag or "-") }` emitted the raw template and every argument as separate lines on the invoking message.

- 2026-10-20 - `lua-execute` syntax sweep - the four repro lines were re-run on AsyncLua 0.7.0.0; all
  returned 2. Status changed to fixed on the engine upgrade.

## What I did instead

Assign every formatted string to a **local** before placing it in the table, so no call is the trailing field:

```lua
local header = string.format("**%s**", title)
local meta = string.format("level=%s · weight=%s · pin=%s · tag=%s",
    level, tostring(weight), tostring(pin), tag or "-")
local lines = { header, meta }
```

The local form was verified correct in the same run (`#{"h", v}` = 2). Parentheses are **not** an alternative - they
were measured broken.

## Proposed fix

Cheapest first:

1. Fix table-constructor evaluation: only a multret expression in the **last** field may expand; a parenthesized
   expression must always truncate to one value.
2. If the fix is not imminent, document the limitation where Lua authors meet it - next to `lua-execute` and in the
   `tool-authoring` skill: "never leave a function call (even a parenthesized one) as the last element of a table
   constructor; assign it to a local first".

## Unverified

- Not traced into the AsyncLua interpreter / table-constructor code: AsyncLua is a sibling repository, not this one, and
  only `StringLibrary` was inspected before the cause was left to behaviour.
- The exact trigger: a nested call among the trailing call's arguments is the best correlation seen, but it is not
  fully pinned (a non-string **literal** argument did not trigger in a clean run).
- Whether the effect is **contagious** - one probe series showed every subsequent constructor expanding after the first
  bad one, but a later clean run did not reproduce it. Reported as observed, not as established.
- Whether user-defined functions returning several values (the real Lua multret case) are affected too - untested; only
  builtin calls were probed.
