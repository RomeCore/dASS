# AR-0005 - Lua `function a.b(...)` declaration crashes the parser with a bare NullReferenceException

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** API (`lua-execute` / AsyncLua parser)
- **First seen:** 2026-10-04

## What I expected

`lua-execute` parses ordinary Lua 5.5. Declaring a method on a table with the
classic dotted-function statement is idiomatic Lua:

```lua
local vm = {}
function vm.bump(self) ... end
```

The manuals for `dass.ui.create_control` describe commands as "method semantics"
(`function(self)` / `function(self, param)`), so this is the natural way to attach
a command closure to the view-model table before packing it. I expected it to run.

## What happened

The tool aborted before executing a single line, with a raw runtime exception, not
a parse error:

```
Caught error: System.NullReferenceException: Object reference not set to an instance of an object.
   at RCParsing.ParsedRuleResult.GetChild(Int32 index) ...
   at RCParsing.ParsedRuleResultBase.GetIntermediateValue[T](Int32 index) ...
   at AsyncLua.Parsing.AsyncLuaParser.<>c.<DeclareStatements>b__10_29(ParsedRuleResultBase v)
   ...
   at AsyncLua.Parsing.AsyncLuaParser.Parse(String input)
   at AsyncLua.LuaState.ExecuteAsync(String code, String sourceName, ...)
   at LLMDesktopAssistant.Scripting.Lua.LuaService.ExecuteAsync(...)
```

There is no source position and no hint that the input is a syntax problem - it
reads as an internal crash, so the natural first hypothesis is "my script body
threw at runtime", which sends the hunt in the wrong direction.

Bisected to the declaration form itself (each run is the entire script):

| Script | Result |
|---|---|
| `function foo() return 1 end` | OK |
| `local function foo() return 2 end` | OK |
| `function vm:bump() return 4 end` (colon) | OK |
| `function vm.bump() return 3 end` (dot) | **NRE** |
| `local vm = {} function vm.bump(self) end` | **NRE** |

So the crash is specific to a **function declaration whose name is a field path
written with a dot** (`function a.b(...)`). The colon method form and plain/local
function declarations are fine.

## Why it costs

An agent that writes idiomatic Lua hits a hard crash whose message points at the
host application, not at the offending construct. Because the error arrives as a
NullReferenceException with no position, the agent cannot tell it is a syntax
issue at all - it must bisect the script by hand to discover which line is
rejected. In this session that isolation cost ~6 extra `lua-execute` calls
(rewriting the script into throwaway probes and tracing via `fs.write`, since
`print` output is discarded whenever the script errors).

Once known, the workaround is a one-token change, so the lasting cost is the
diagnosis, not the fix. But the trigger is exactly the shape an agent reaches for
when following the `lua-ui-controls` command pattern, so the diagnosis cost is
paid again by every fresh session.

## Repro

```lua
local vm = {}
function vm.bump(self) end
return "never reached"
```

Contrast (both succeed):

```lua
local vm = {}
function vm:bump() end
```

```lua
local vm = {}
vm.bump = function(self) end
```

## Witnesses

- 2026-10-04 - session demonstrating the Lua API - building a dynamic Avalonia
  control (`dass.ui.create_control` + `dass.tool.result.append_data`) with command
  members declared as `function vm.bump(self)`. The tool crashed twice with the NRE
  above; the failure was first misattributed to the UI binding layer and only
  isolated to the declaration syntax after bisecting with file-traced probes.

## What I did instead

Rewrote every command from

```lua
function vm.bump(self) ... end
```

to

```lua
vm.bump = function(self) ... end
```

(the colon form `function vm:bump() ... end` also works). The dynamic control then
rendered and its commands bound correctly.

## Proposed fix

1. **Support the syntax.** Make the `DeclareStatements` transform produce a
   `a.b` (dotted) function name instead of dereferencing a child that does not
   exist. Standard Lua must accept `function a.b(...)` and `function a.b.c(...)`;
   the parser currently falls over on the one-level case already.
2. **Fail loudly and legibly.** Independently of (1), the parser should never leak
   an NRE. Guard the child access (`GetChild` / `GetIntermediateValue`) in the
   `DeclareStatements` transforms, or wrap parse-time transforms so a malformed /
   unrecognised construct surfaces as a `ParsingException` with a position - the
   useful kind of message this parser already knows how to produce (compare the
   `'>' is unexpected character, expected one of: ...` output seen for other bad
   input).
3. **Document until fixed.** If (1) is not immediate, note in the `lua-execute`
   tool description that dotted function *statements* are unsupported and that
   `vm.name = function(self) end` should be used instead.

Cause is **traced** from the stack: `AsyncLuaParser.DeclareStatements` ->
`ParsedRuleResultBase.GetIntermediateValue` -> `ParsedRuleResult.GetChild` NRE.

## Unverified

- Whether `function a.b.c(...)` (two or more levels) behaves the same - only the
  one-level dotted form was tested.
- Whether the same transform is reachable from other constructs the tool parses.
- Whether the colon form is genuinely supported or merely happened to parse in the
  probe; only `function vm:bump() end` was exercised.
- The exact grammar rule at fault was not opened in the AsyncLua source from this
  session - the location is taken from the stack trace only.
