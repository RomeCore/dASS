# AR-0017 - Standard Lua call sugar (`f "str"`, `f{...}`) is rejected, so `print "x"` and `require "mod"` fail

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (`lua-execute` / AsyncLua parser)
- **First seen:** 2026-10-20

## What I expected

`lua-execute` advertises "AsyncLua 5.5+0.7.0.0", an extended Lua 5.5 interpreter. In Lua, a call may omit
the parentheses when the single argument is a string literal or a table constructor:

```lua
print "hello"
local cfg = require "config"
local widget = make{ name = "x", value = 1 }
```

These are the standard `args ::= '(' [explist] ')' | tableconstructor | String` forms, and they are
idiomatic in Lua code (especially `require "mod"` and `print "msg"`).

## What happened

Both sugar forms are rejected, in statement *and* expression position.

Statement position:

```lua
print "hello"
```

```
Caught error: RCParsing.SemanticException: A semantic error occurred: Expected a function call as a statement.

Location:
print "hello"
^^^^^ line 1, column 1, length 5
```

Even `print 'string call sugar'` fails with the same message. The parser treats `print` as a postfix
expression and then rejects it because it is not a *parenthesised* call; the string argument is never
consumed.

Expression position:

```lua
local f = function(x) return x .. '!' end
local r = f 'hi'
print(r)
```

```
Caught error: RCParsing.SemanticException: A semantic error occurred: Expected a function call as a statement.

Location:
local r = f 'hi'
            ^^^^ line 2, column 13, length 4
```

Table argument:

```lua
local f = function(x) return x.a end
local r = f{ a = 5 }
print(r)
```

```
Caught error: RCParsing.ParsingException: An error occurred during parsing:
'{' is unexpected character, expected one of:
  literal ':' ... literal '(' ...
```

The parser's `args` rule accepts only `(`.

## Why it costs

- LLM priors are full of `print "..."` and `require "..."`; every such line is a hard parse failure, and
  the whole script never executes. The fix is one pair of parentheses, but the agent must first realise that
  the line is the problem.
- The error message points at `print`/`f`, not at the string argument, and calls it "a statement" problem.
  In a long script this sends the hunt to the wrong line.
- `require "mod"` cannot be tested at all, so it is unknown whether the module system exists.

## Repro

```lua
print "hello"
```

```lua
local f = function(x) return x .. '!' end
print(f 'hi')
```

```lua
local f = function(x) return x.a end
print(f{ a = 5 })
```

All three fail before any line executes. Contrast (all succeed):

```lua
print("hello")
local f = function(x) return x .. '!' end
print(f('hi'))
print(f({ a = 5 }))
```

## Witnesses

- 2026-10-20 - `lua-execute` syntax sweep - 30-construct parser test; the sugar forms were the only call
  syntax that failed, and they failed in both statement and expression position.

## What I did instead

Wrote every call with explicit parentheses. For `require`, did not use it at all; the module system is
unverified.

## Proposed fix

Cheapest first:

1. **Support the sugar.** Extend the parser's `args` rule with `String` and `tableconstructor` alternatives,
   in both statement calls and expression postfix chains - standard Lua requires both.
2. **Improve the diagnostic until fixed.** When a string/table immediately follows an identifier in call
   position, report "call sugar is not supported; add parentheses around the argument" instead of
   "Expected a function call as a statement" pointing at the callee.
3. **Document the subset** in the `lua-execute` description and the `lua-script-authoring` skill:
   "always use parentheses; `print \"x\"` and `require \"mod\"` do not parse".

## Unverified

- Whether AsyncLua has a `require` implementation at all; the sugar failure prevented reaching it.
- Whether the colon form `obj:method "x"` is affected in the same way (not tested separately).
- Whether the same parser rule is reachable from scriptable tools/commands, or only from `lua-execute`
  (they share `LuaService`, so likely the same, but not exercised from a tool).
