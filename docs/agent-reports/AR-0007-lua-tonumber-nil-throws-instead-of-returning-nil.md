# AR-0007 - `tonumber(nil)` threw instead of returning nil, so the idiomatic `tonumber(arg) or default` failed

- **Status:** fixed
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (`lua-execute` / AsyncLua `tonumber`)
- **First seen:** 2026-10-04

## What I expected

In standard Lua, `tonumber` never throws for a non-convertible value - it returns `nil`:

```lua
tonumber(nil)    -- nil
tonumber("abc")  -- nil
tonumber("42")   -- 42
```

So the idiomatic default works:

```lua
local line_cap = tonumber(tool_args.maxFileLines) or 0
```

## What happened

**Original sighting (2026-10-04, AsyncLua 0.5.2).** A Lua tool declared two optional properties, `path`
and `maxFileLines` (no defaults), and was called with `path` only. Its body ran:

```lua
local line_cap = tonumber(args.maxFileLines) or 0
```

and the call came back as:

```
Caught error: tonumber: cannot convert to number
Location (at '(args.maxFileLines)'):
local line_cap = tonumber(args.maxFileLines) or 0
                         ^^^^^^^^^^^^^^^^^^^ line 3, column 26, length 19
```

The original report inferred that the omitted argument arrived as a **non-nil null** and that `tonumber`
refused it. **That inference was wrong** (corrected by the maintainer). `args.maxFileLines` was a normal
Lua `nil`; the exception came from `tonumber(nil)` itself, which AsyncLua 0.5.2 implemented as a hard
error instead of returning `nil`.

**Update 2026-10-20 (AsyncLua 0.7.0).** The nil case is fixed:

```lua
print(tonumber(nil))        -- nil
print(tonumber(nil) or 0)   -- 0
```

The original repro now behaves as standard Lua requires.

## Why it costs

- The error was misleading: it read as "you passed a non-number", but nothing was passed at all. Finding
  that out cost a debugging round.
- `tonumber(x) or default` is the idiomatic Lua guard, and it silently did not work - every author of a Lua
  tool with an optional argument had to re-derive a type-guard workaround.
- It hid in tools that never call `tonumber`: any `x or default` pattern was a landmine because the engine
  threw before the `or` could fire.

## Repro

Original (AsyncLua 0.5.2) - throws:

```lua
print(tonumber(nil))
```

Current (AsyncLua 0.7.0) - fixed:

```lua
print(tonumber(nil))        -- nil
print(tonumber(nil) or 0)   -- 0
```

## Witnesses

- 2026-10-04 - git-full-diff session - `git-full-diff` called with `path` only, while its schema also
  declared `maxFileLines`; the third line of the body was `tonumber(args.maxFileLines) or 0`.
- 2026-10-20 - `lua-execute` feature test - `tonumber(nil)` and `tonumber(nil) or 0` were re-run on
  AsyncLua 0.7.0.0 and now return `nil` / `0`. Status changed to fixed on the engine upgrade. The original
  "non-nil null" diagnosis is retracted: the maintainer confirmed the failure was `tonumber(nil)` throwing.

## What I did instead

Originally every argument read was guarded by type instead of by nil-ness:

```lua
local repo = "."
if type(args.path) == "string" and args.path ~= "" then repo = args.path end
```

After the 0.7.0 upgrade, the idiomatic form works again:

```lua
local line_cap = tonumber(args.maxFileLines) or 0
```

## Proposed fix

Fixed by the AsyncLua 0.7.0 upgrade - `tonumber(nil)` returns `nil` and `or default` fires. No dASS-side
change is needed.

## Unverified / follow-up

- **`tonumber("abc")` still throws** on AsyncLua 0.7.0 (`tonumber: cannot convert to number`), whereas
  standard Lua returns `nil`. The nil case is fixed; the non-numeric-string case is a separate, still-open
  divergence and may deserve its own report.
- The original mechanism was never traced; the cause was supplied by the maintainer after the fact, not
  established from source. The earlier "unmarshalled null" hypothesis (and its `LuaNil` sentinel variant) is
  retracted rather than kept as a possibility.
- Not re-tested from a scriptable tool with an `argument_schema`; only `tonumber` was exercised through
  `lua-execute`.
