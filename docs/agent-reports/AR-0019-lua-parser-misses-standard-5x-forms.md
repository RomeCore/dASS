# AR-0019 - The parser accepts a narrower Lua subset than the advertised 5.5: table `;`, hex floats, unary `~`, `<const>/<close>`, and `\z`

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (`lua-execute` / AsyncLua parser)
- **First seen:** 2026-10-20

## What I expected

The tool description says "Lua is executing using AsyncLua 5.5+0.7.0.0", and the engine manual calls itself
"an extended Lua 5.5 interpreter". Lua 5.5 therefore should accept the standard forms introduced in 5.2-5.4:

| Construct | Introduced | Expected |
|---|---|---|
| `{ 1, 2; 3 }` | always | `;` is an alternative field separator |
| `0x1p4` | 5.2 | hex float = 16 |
| `~x` | 5.3 | unary bitwise NOT |
| `local x <const> = 1`, `local f <close> = ...` | 5.4 | variable attributes |
| `"a\z\n   b"` | 5.2 | `\z` skips the following whitespace, result `"ab"` |

## What happened

Every one of them is rejected or misbehaves:

| Script | Result |
|---|---|
| `local t = { 1, 2; 3 }` | Parse error: `';' is unexpected character` |
| `print(0x1p4)` | Parse error: `'p' is unexpected character` |
| `print(~0)` | Parse error: `'~' is unexpected character`; the expected-token list has `minus`, `#`, `await\|not` but no unary `~` |
| `local x <const> = 5` | Parse error: `'<' is unexpected character` |
| `print('a\z\n   b')` | **No error**, but prints `az\n   b` - `\z` is not recognised and does not skip whitespace |

Binary bitwise operators (`&`, `|`, `~`, `<<`, `>>`) work, and hex integers (`0xFF`) work, so the gap is
specifically the unary and literal forms.

## Why it costs

- Any Lua ported from another host can contain `{ a, b; c, d }`, hex floats, `~mask` or `local x <const>`.
  Each one aborts the whole script before a single line runs, so a single unsupported form hides every
  other problem in the file.
- `\z` is the worst case: it does not fail, it quietly produces the wrong string. String builders that wrap
  long format strings with `\z` get stray `z` characters and unstripped indentation.
- The manual says 5.5, so the author has no reason to suspect these forms. Each discovery costs a
  bisection round, because a parse error kills the entire script and discards all `print` output.

## Repro

```lua
print(#({ 1, 2; 3 }))          -- parse error on ';'
print(0x1p4)                    -- parse error on 'p'
print(~0)                       -- parse error on '~'
local x <const> = 5             -- parse error on '<'
print('[' .. 'a\z
   b' .. ']')                   -- prints [az
                                --    b], expected [ab]
```

## Witnesses

- 2026-10-20 - `lua-execute` syntax sweep - 30-construct parser test covering declarations, loops,
  functions, tables, literals, escapes and operators; these five forms were the only standard-Lua misses.

## What I did instead

Used the 5.1-compatible subset: commas in table constructors, decimal/exponent floats, no unary bitwise NOT
(the expression had to be reformulated), plain `local` without attributes, and string concatenation or a
long bracket string instead of `\z`.

## Proposed fix

Cheapest first:

1. **Fix `\z` first** - it is silent and cheap to implement (skip whitespace after the escape). A wrong
   string is worse than a parse error.
2. **Add the missing parser forms**: `;` as a table field separator, hex floats, unary `~`, and the
   `<const>/<close>` attribute syntax. These are small grammar additions.
3. **Document the subset** in the `lua-execute` description and the `lua-script-authoring` skill until the
   parser catches up: "no `;` field separators, no hex floats, no unary `~`, no variable attributes, `\z`
   is broken".

## Unverified

- `<close>` was not tested (only `<const>`); both are 5.4 attributes and likely share the same gap.
- Whether `\z` is a parser or a string-library bug; only its behaviour was observed.
- Other 5.2-5.4 additions were not enumerated (`goto` works, `//` works, bitwise binary works); the list is
  what was probed, not a complete conformance audit.
- Whether scriptable tools/commands share the same parser build - they share `LuaService`, but only
  `lua-execute` was exercised.
