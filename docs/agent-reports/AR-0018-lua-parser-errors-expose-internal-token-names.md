# AR-0018 - Lua parser errors expose internal rule/token names as if they were keywords, and the call-statement fallback misreports the location

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** API (Lua parser diagnostics)
- **First seen:** 2026-10-20

## What I expected

When `lua-execute` rejects a script, the "expected one of" list should name Lua tokens and keywords that the
author can actually type: `if`, `while`, `function`, `return`, `identifier`, `number`, `string`, `(`,
`{`, `...`, and so on.

## What happened

The list includes **internal rule and token names** that look like Lua keywords but are not:

```
'>' is unexpected character, expected one of:
  keyword 'if'
  keyword 'while'
  'varscope'
  keyword 'async'
  ...
  literal choice '*|/|//|%'
  'minus'
```

`varscope` is not a keyword. It is the internal name of the token that matches `local` or `global`
(traced: `AsyncLuaParser.cs`, `builder.CreateToken("varscope").KeywordChoice(("local", ...), ("global", ...))`).
The statement rule wires it as a scoped declaration, so the user-facing syntax is:

```lua
global x = 10
global async function f() ... end
```

Not `varscope local x = 10`.

I only discovered this by opening the AsyncLua grammar after several failed calls:

```
varscope local x = 1   -- SemanticException: Expected a function call as a statement
varscope global x = 1  -- same
varscope do ... end    -- same
varscope               -- same
```

The same error is produced for a **different** problem - the missing call sugar (AR-0017):

```lua
local r = f 'hi'
```

```
SemanticException: Expected a function call as a statement.
Location:
local r = f 'hi'
            ^^^^ line 2, column 13, length 4
```

The location points at `f`, not at the string argument, and the message says "statement" although the
failure is in the right-hand side of a local declaration.

## Why it costs

- An agent reading "expected ... 'varscope'" reasonably concludes that `varscope` is a Lua keyword and tries
  to write it. Each attempt is a full `lua-execute` round; I burned four calls on `varscope` forms before
  looking at the grammar.
- The internal names (`minus`, `postfix_expr`, `literal choice '*|/|//|%'`) also leak implementation details
  into the model context, which costs tokens and teaches the wrong vocabulary.
- The call-sugar error (AR-0017) is indistinguishable from a real statement-position error, so the same
  message sends authors to the wrong fix.

## Repro

```lua
varscope local x = 1
```

```lua
local r = f 'hi'
```

Both produce "Expected a function call as a statement" rather than a message that names the actual construct.

## Witnesses

- 2026-10-20 - `lua-execute` syntax sweep - probing parser coverage; the `varscope` token name appeared in
  the expected-token list and was mistaken for a keyword, costing four calls.

## What I did instead

Opened `<AsyncLua repo>\src\AsyncLua\Parsing\AsyncLuaParser.cs` (the engine, not dASS) to read
the token definition and the statement wiring, then used the real syntax `global x = ...`. Without source
access the feature is effectively undiscoverable from the error text.

## Proposed fix

Cheapest first:

1. **Map internal names to user-facing text** in parse errors: `varscope` -> "`local` or `global`",
   `minus` -> "`-`", `literal choice ...` -> the literal operators, and so on. A small display-name table
   next to the token declarations.
2. **Do not list `varscope` at all** if `global`/`local` are already listed; the internal token should not
   be user-visible.
3. **Special-case the call-statement fallback.** When the failing expression is an identifier followed by a
   string or `{`, say "call sugar is not supported; add parentheses" and point at the argument.
4. Document `global`/`local` scope forms in the `lua-execute` description or the AsyncLua manual (they are
   absent from `manuals(asynclua)`).

## Unverified

- Whether `varscope` is documented anywhere user-facing; `manuals(asynclua)` does not mention it.
- Whether the `minus`/`postfix_expr` names appear in other error paths or only in the expected-token list.
- The exact fallback path that turns a parse failure into "Expected a function call as a statement" was not
  traced beyond the statement rule; the report is behaviour-first, with the token definition traced.
