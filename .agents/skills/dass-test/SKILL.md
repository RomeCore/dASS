---
name: dass-test
description: Test dASS itself as a black box. Use when the user asks to check, test, probe or verify dASS - its tools, addons, skills, prompts, UI, localization or behaviour - or when the user hands over a list of steps/commands to run against the running app. Keeps the agent driving the app from the outside and obeying the given steps - no reading the dASS source without a heads-up and permission, no extra steps, no unasked refactors, builds or fixes.
---

# dASS test (dASS)

In this mode **the app is the subject under test, and I am the tester.** The source is a last resort, not the first move. My job is to drive dASS from the outside, watch what it does, and report it plainly. Reading the code to answer "how does it work" is the failure mode this skill exists to stop.

## When it is active

- The user asks to check / test / probe / verify dASS, or any part of it.
- The user hands over a numbered list of steps to run against the app.
- I am about to open dASS source to answer a question about its behaviour.
- The user says something like "just do it / follow my commands".

## Three rules

| # | Rule | Failure it prevents |
|---|---|---|
| 1 | **Black box first.** Answer behavioural questions by *driving the app* - call the tool, open the panel, read the returned text. | Explaining behaviour from source instead of from what the app did |
| 2 | **No source without a heads-up.** If the cause truly needs the source, say one line first - "this needs a look at source, may I?" - and wait for a yes. | Silently wandering into the repo mid-test |
| 3 | **Obey, don't expand.** Do exactly the asked step. No refactors, no "while I'm here", no fixes, no extra calls. Note other oddities; do not act on them. | Turning a test into an unasked dev session |

## Obedience

- The user's points are the **test script**. Run them. One step at a time unless told to batch.
- Do the **literal** thing. If a step looks wrong, ask in one line - never substitute my own plan for it.
- **"Extra moves" = any call, file read, edit or build beyond the asked step.** There are none.
- Do not slip into code mode: no diffs, no `dotnet build`, no patches - unless ordered.

## Observing from the outside

- **Tools / APIs:** call them the way the user would, then quote the actual result. The output is the evidence.
- **UI / localization:** only the user can see it - ask what is on screen and record their words. (Same split as `dass-report`'s user lane.)
- **Addons / skills / prompts:** observe what *loads* and what it *declares*; do not trace how it is wired internally.

## Reporting

- One line for the outcome: **expected vs observed**.
- A mismatch is a finding, not a task - hand it to `dass-report` if the user wants it filed.
- Do not narrate every step. Report at the end, or when something breaks.

## When the source *is* on the table

If the user explicitly permits looking at or changing dASS code, this skill yields to normal dev mode - but still **warn before touching the repo**: a change needs a rebuild to take effect and does **not** affect the running instance. After any code dig, come back and report behaviourally.

## Not here

- Filing a bug about dASS → `dass-report`.
- Writing a fix as an addon → `equip`.
- A feat worth remembering → `record-achievement`.
