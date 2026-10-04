# AR-0006 - Lua pattern captures around a negated character class silently fail to match

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** API (AsyncLua `string.match` / `string.gmatch`)
- **First seen:** 2026-10-04

## What I expected

`lua-execute` advertises "Lua is executing using AsyncLua 5.5+0.5.2.0", so `string.match`/`string.gmatch` should follow Lua 5.5 pattern semantics. Under those semantics

```lua
string.match("12\t3\tfoo/bar.txt", "^([^\t]+)\t([^\t]+)\t(.*)$")
```

returns three captures - the idiomatic way to parse a tab-separated line.

## What happened

The capture fails, or matches text the class should have excluded. Measured, in one `lua-execute` run:

| Call | Result | Expected |
|---|---|---|
| `string.match("abc", "([^z]+)")` | `nil` | `"abc"` |
| `string.match("abc", "[^z]+")` | `"abc"` | `"abc"` |
| `string.match("12\t3\tfoo/bar.txt", "^([^\t]+)\t([^\t]+)\t(.*)$")` | `nil` | `"12"`, `"3"`, `"foo/bar.txt"` |
| `string.match("a\nb", "([^\n]+)")` | `"\n"` | `"a"` |
| `string.gmatch("a\nb\nc", "([^\n]+)")` | `"\n"`, `"\n"` | `"a"`, `"b"`, `"c"` |
| `string.gmatch("a\nb\nc", "[^\n]+")` | `"a"`, `"b"`, `"c"` | `"a"`, `"b"`, `"c"` |

The rule that fits every row: **a capture `(...)` wrapping a negated class `[^...]` breaks; the same class without a capture works.** Captures over `%S+`, `%d+` and `.` work normally.

Cost in this session, concretely: `git-full-diff` parses `git diff --numstat` with `string.match(line, "^([^\t]+)\t([^\t]+)\t(.*)$")`. Every line returned `nil`, the tool fell through to "no tracked files", and it reported **"4 files changed, +831 / −0"** while the diff printed directly underneath clearly contained tracked changes. The wrong answer was well-formed and plausible; nothing warned.

## Why it costs

Per occurrence:

- **Silent wrong results** in any Lua that parses text with the most common shape there is (negated class + capture). The output looks legitimate, so it ships.
- One debugging round per occurrence: I burned four probes bisecting the pattern before the shape emerged, then rewrote the parsing onto `regex`.
- It poisons trust in `lua-execute` for *all* string work - after this, every pattern has to be treated as suspect.

## Repro

```lua
print(string.match("abc", "([^z]+)"))   -- nil, expected "abc"
print(string.match("abc", "[^z]+"))     -- abc
```

## Witnesses

- 2026-10-04 - git-full-diff session - parsing `git diff --numstat` output with `string.match(line, "^([^\t]+)\t([^\t]+)\t(.*)$")`; the tool silently reported 0 tracked files out of 2.

## What I did instead

Replaced every capture-around-a-class pattern with the `regex` namespace (`regex.match`, `regex.split`, `m.groups[n].value`), which behaves correctly. Every Lua author pays this translation until the engine is fixed or the limitation is documented.

## Proposed fix

Cheapest first:

1. Fix the pattern engine so a capture around a negated class behaves as Lua 5.5 does.
2. If the fix is not imminent, document the limitation where Lua authors will meet it - next to `lua-execute`, and in the `tool-authoring` skill - with "prefer `regex` for captures around `[^...]`".

## Unverified

- Not traced into the AsyncLua pattern implementation: AsyncLua is not in this repository, and the cause was established from behaviour only.
- Whether *positive* classes under a capture (`([abc]+)`) are affected too - untested; `%S+`, `%d+` and `.(*)` captures work.
- Whether it depends on the host build (Desktop vs non-Desktop) - untested.
