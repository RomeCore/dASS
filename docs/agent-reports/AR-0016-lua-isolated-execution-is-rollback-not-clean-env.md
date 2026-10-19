# AR-0016 - `isolatedExecution=true` still reads globals written by non-isolated scripts and silently rolls back its own writes

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `lua-execute` / API (execution isolation)
- **First seen:** 2026-10-20

## What I expected

The `lua-execute` description says:

> Executes Python in isolated virtual environment (global variables are not accessible between scripts)

For `isolatedExecution=true` (the default) I read that as: the script starts in a **clean environment**, so
globals from earlier calls are not visible, and nothing this call writes can leak into later calls.

## What happened

Measured across four separate `lua-execute` calls:

| # | `isolatedExecution` | Script | Observed |
|---|---|---|---|
| 1 | `false` | `persist_probe = 123` | `persist_probe` is now `123` in the shared state |
| 2 | `false` | `shared_counter = (shared_counter or 0) + 1` | `shared_counter == 1` |
| 3 | `true` | `print(persist_probe, shared_counter)` | **`123`, `2`** - the isolated call *sees* the shared globals |
| 4 | `true` | `persist_probe = 999; shared_counter = shared_counter + 100` | inside the call: `999`, `102` |
| 5 | `false` | `print(persist_probe, shared_counter)` | **`123`, `2`** - the isolated writes were rolled back |

A variable created only inside an isolated call is invisible to the next isolated call (`iso_probe` -> `nil`),
so the rollback itself works. But the isolated call is **not** a fresh environment: it starts from the
shared state and discards its writes afterwards.

## Why it costs

- An agent that trusts "global variables are not accessible between scripts" can accidentally depend on a
  global left by an earlier non-isolated call. The result is correct-looking but not reproducible: the same
  script gives a different answer after a different history.
- Idempotent-looking code (`counter = (counter or 0) + 1`) silently starts from an inherited value under
  `isolatedExecution=true`. That is the silent failure class - the output is plausible.
- The workaround is to treat isolated calls as read-only over shared state and never define globals in
  non-isolated calls, which is an undocumented discipline.

## Repro

```lua
-- call 1, isolatedExecution = false
persist_probe = 123
shared_counter = (shared_counter or 0) + 1

-- call 2, isolatedExecution = true
print(persist_probe)          -- 123, although the globals should not be "accessible between scripts"
persist_probe = 999
shared_counter = shared_counter + 100

-- call 3, isolatedExecution = false
print(persist_probe)          -- 123: the isolated call's writes were rolled back
print(shared_counter)         -- 1: same
```

## Witnesses

- 2026-10-20 - `lua-execute` feature test - probing the `isolatedExecution` flag after the basic API run;
  the mismatch was found before any real script depended on it.

## What I did instead

Treated `isolatedExecution` as "transactional / rollback" rather than "fresh environment": read shared
globals deliberately, and do not rely on a clean namespace. For throwaway probes, used unique variable names.

## Proposed fix

Cheapest first:

1. **Reword the description** to what the flag actually does: "isolated execution starts from the shared
   environment and discards any globals it writes; it does not hide globals written by non-isolated calls."
   This removes the false safety assumption with no code change.
2. If true isolation is intended, snapshot the environment before the call and restore it afterwards
   (including hiding globals), instead of only rolling back writes.
3. Add a one-line example to the `lua-script-authoring` skill showing the difference between the two modes.

## Unverified

- The scope of the shared state was not checked across chats or across app restarts - only within one chat
  session.
- Whether concurrent `lua-execute` calls (two scripts at once) can observe each other's partial writes; the
  flag name suggests a lock/snapshot exists, but only sequential calls were tested.
- The exact meaning of `isolatedExecution=false` for a script that throws halfway: whether earlier writes are
  kept was not tested.
