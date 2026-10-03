# AR-0004 - shipped skills tell the agent to use `time-get`, which is not in the agent's toolset

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** prompt (skill `record-achievement`; same reference in skill `equip`)
- **First seen:** 2026-10-04

## What I expected

The `record-achievement` skill treats the date as mandatory and sourced from a tool:

- rule: "**Dates come from `time-get` only.** A guessed date breaks sorting and trust."
- workflow step 2: "Get the current date (`time-get`)."
- checklist: "The date came from `time-get`, not from imagination."

Read as a contract, an agent loading this skill should be able to call `time-get` and get the date.

## What happened

`time-get` is not present in the agent's advertised toolset. It is not in the tools listed by `addon-list_available`, and it is not among the hidden tools this runtime declares. So the literal instruction - "get the date from `time-get`" - cannot be followed by the agent as written.

What *does* exist is a native **chat-agent** tool registered by `src/LLMDesktopAssistant/Tools/Implementations/TimeToolModule.cs` (`Name = "time-get"`, `[ToolModule(chatScoped: false)]`). It is reachable by the chat agent's tool-calling loop, but not exposed to the desktop-assistant agent that the skill is written for. `equip`'s step 7 carries the same bare reference ("The date comes from `time-get`").

The gap is invisible until a skill is actually used: the agent loads the skill, reaches the date step, and finds no such tool.

## Why it costs

- The skill's own mandatory rule cannot be satisfied from where the agent stands. The agent must either notice the gap and substitute (this session: `shell-powershell Get-Date -Format 'yyyy-MM-dd'`) or risk doing the thing the skill explicitly forbids - guessing the date.
- It is a trap that fires exactly at the moment of writing an entry, when attention is on the content, not on tool availability.
- Two skills share the reference, so the cost recurs every time either is used.

## Repro

1. Load the skill `record-achievement`.
2. Reach step 2 ("Get the current date (`time-get`)").
3. Look for `time-get` in the agent's toolset - it is absent (see `addon-list_available`).

## Witnesses

- **2026-10-04** - session that recorded `ACH-0001` (the power-outage draft survival) - had to abandon `time-get` and use `shell-powershell Get-Date -Format 'yyyy-MM-dd'` to honour the "no guessed date" rule.

## What I did instead

Obtained the date via `shell-powershell` (`Get-Date -Format 'yyyy-MM-dd'`), which satisfies the intent (a real date, not a guess) but contradicts the letter ("from `time-get` only").

## Proposed fix

1. **Make the date obtainable as the skill promises.** Either expose `time-get` to the desktop-assistant agent's toolset, or reword the skill to a mechanism the agent actually has (e.g. "get the date via your shell / `fs` / `lua` API").
2. **If the reword route:** keep the intent ("never invent a date") and drop the tool name, so the rule cannot rot when the tool list changes.
3. **Sweep the other skills** for the same bare `time-get` reference (at least `equip`) and reconcile them in one pass.

## Unverified

- Whether `time-get` is hidden behind a per-context flag here, or simply not wired into the desktop-assistant toolset at all, was not checked in code beyond the module registration above.
- Whether other builds/loadouts of dASS *do* expose `time-get` to the agent is unknown.
- The `TimeToolModule` registration was read from source (traced); the tool's absence from the live toolset was observed directly.
