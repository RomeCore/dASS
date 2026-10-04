# AR-0007 - an omitted Lua tool argument arrives as a non-nil null value, so `tonumber()` throws

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (Lua tool arguments, `tool_args`)
- **First seen:** 2026-10-04

## What I expected

When a Lua tool is called and an *optional* property declared in its `argument_schema` is omitted, `tool_args` should simply not carry that key - i.e. Lua `nil`. Then the idiomatic default works:

```lua
local line_cap = tonumber(tool_args.maxFileLines) or 0
```

## What happened

`git-full-diff` declared `path` and `maxFileLines` (both optional, no default) and was called as `{"path": "."}`. The third line of the body was:

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

In Lua, `tonumber(nil)` returns `nil` and raises nothing - so `args.maxFileLines` was **not** `nil`. It was a live, truthy value that `tonumber` refused, which also means the `or 0` never fired. Reading a key that was never sent yields something that is neither `nil` nor usable by ordinary Lua string/number operations.

## Why it costs

Per occurrence:

- The error is misleading. It reads as "you passed a non-number", but nothing was passed at all; the real cause is an unmarshalled null. Finding that out costs a debugging round.
- `x or default` is the idiomatic Lua default and it silently does not work here - the guard has to be re-derived by every author of every Lua tool with an optional argument.
- It hides in tools that never call `tonumber`: `args.x ~= nil` is true for a value that was not supplied, so branches intended for "argument omitted" never run.

## Repro

A Lua tool whose `argument_schema` declares two optional properties, called with one of them omitted, whose body runs `tonumber(tool_args.omitted)` (or any `args.omitted or default` / `args.omitted ~= nil` check).

## Witnesses

- 2026-10-04 - git-full-diff session - `git-full-diff` called with `path` only, while its schema also declared `maxFileLines`.

## What I did instead

Guarded every argument read by type instead of by nil-ness:

```lua
local repo = "."
if type(args.path) == "string" and args.path ~= "" then repo = args.path end
```

## Proposed fix

Cheapest first:

1. Convert JSON `null` in tool arguments to Lua `nil`, so `args.x or default` works as in every other Lua host.
2. Failing that, make the *read* of such a value raise a clear error naming the argument ("argument 'maxFileLines' was not provided"), instead of letting it surface as `tonumber: cannot convert to number`.
3. Failing both, document it in `tool-authoring`: read optional arguments with `type(...)`, never `x or default` - the contract text is what the model reads before writing the body.

## Unverified

- The mechanism was not traced. Two candidates: the invocation layer serialises every declared property (null for the omitted ones), or AsyncLua's table indexer returns a `LuaNil` sentinel that reads back as a truthy value. The report is written from behaviour, not from source.
- Not tested when the omitted property is absent from the schema entirely.
- Not tested on the Python or C# script tool engines - only the Lua engine.
