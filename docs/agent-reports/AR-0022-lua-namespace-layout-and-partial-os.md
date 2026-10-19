# AR-0022 - The Lua API surface is split between root namespaces and `dass.*`, and `os` is only partially implemented

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** API (`lua-execute` namespaces)
- **First seen:** 2026-10-20

## What I expected

`namespaces()` lists a flat set of names:

```
_G
dass
dass.agents
asynclua
dass.chat
dass.command
crypto
datetime
fs
icon
image
json
dass.log
dass.models
os
process
random
regex
dass.templates
time
dass.toasts
dass.tool
dass.tool.result
dass.ui
web
git
```

The tool description and `AGENTS.md` show `dass.chat`, `dass.tool.result` and `dass.ui`, so I expected every
namespace to live under `dass` (`dass.json`, `dass.time`, `dass.fs`). I also expected `os` to be the standard
Lua `os` library, since it is listed without qualification.

## What happened

| Probe | Observed |
|---|---|
| `type(dass.json)` | `nil` |
| `type(dass.time)` | `nil` |
| `type(json)`, `type(time)`, `type(fs)` | `table` / `function` - the namespaces are **root-level** |
| `type(os.clock)` | `nil` |
| `type(os)` | `table` - the namespace exists, but is partial |

The naming is mixed: some interfaces are `dass.*` (`dass.chat`, `dass.command`, `dass.log`, `dass.models`,
`dass.templates`, `dass.toasts`, `dass.tool`, `dass.tool.result`, `dass.ui`, `dass.agents`), while others
are bare (`crypto`, `datetime`, `fs`, `icon`, `image`, `json`, `os`, `process`, `random`, `regex`, `time`,
`web`, `git`).

`os.clock()` fails at runtime with a message that does not mention the missing function:

```
Caught error: AsyncLua.LuaRuntimeException: Call target must be a function or have __call metamethod.
Location (at '()'):
local started = os.clock()
                        ^^ line 6, column 25, length 2
```

`os.clock` is `nil`, so `os.clock()` reads as "call a nil", not "this function is not implemented".

## Why it costs

- The split forces a discovery round: an agent that reads `dass.chat` in the description naturally writes
  `dass.json`, gets `nil`, and then has to call `namespaces()` to learn that `json` is root-level.
- `os` looks like a complete standard library but is not. `os.clock` is the obvious timer for benchmarking a
  Lua script, and it fails with a generic "call target must be a function" message. The workaround
  (`time.now()`, which returns seconds) is not discoverable from `os`.
- Every model writing Lua from priors will reach for `os.clock`, `os.time`, `os.date`; if those are partial,
  the failure mode is the same each time.

## Repro

```lua
print(type(dass.json), type(dass.time))   -- nil nil
print(type(json), type(time))              -- table function
print(type(os.clock))                      -- nil
print(os.clock())                          -- runtime error: call target must be a function
```

## Witnesses

- 2026-10-20 - `lua-execute` feature test - first basic run used `dass.json`/`dass.time` and got `nil`;
  the namespace listing and the `os.clock` failure were recorded in the same session.

## What I did instead

Called `namespaces()` first and used the root names (`json.encode`, `time.now`). For timing, replaced
`os.clock()` with `time.now()`, which returns seconds as a float.

## Proposed fix

Cheapest first:

1. **Document the namespace map** in the `lua-execute` description (or in `manuals(_G)`): which names are
   root-level and which are `dass.*`, and state that `os` is not the standard library.
2. **Either implement `os.clock`/`os.date`/`os.time` or remove the `os` namespace** - a partial standard
   library is worse than an absent one, because it produces "call a nil" instead of "no such namespace".
3. **Consider aliases** (`dass.json = json`, `dass.time = time`) so both spellings work; the mixed layout
   would stop being a trap at almost no cost.

## Unverified

- The complete list of `os` members was not enumerated; only `os.clock` was probed.
- Whether `dass.json`/`dass.time` are deliberately omitted or are missing aliases; no source was opened for
  this report.
- Whether `git` is dASS-specific or comes from another addon; it appears in `namespaces()` but was not
  exercised.
