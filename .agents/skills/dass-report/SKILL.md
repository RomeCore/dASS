---
name: dass-report
description: File a structured report about dASS itself - a native tool that behaved unlike its description, a prompt, an API, or something only the user can see, like the UI or localization. Use when a tool does something other than the model expected, when the same friction is hit a second time, when the user reports a UI or localization problem, or when the user asks for an improvement to be written up.
---

# dASS report (dASS)

A tool's description is its contract, and **the client is always right**: when a tool's behaviour contradicts the plain reading of its description, the contract is what failed. A report is written against the contract, not against the model that misread it.

Every report lands in `docs/agent-reports/` as `AR-NNNN-<slug>.md`. One file, one problem. Write for the maintainer, who was not in this session.

## Two lanes

| Lane | Where the friction comes from | Surfaces |
|---|---|---|
| **agent** | I hit it myself, while working | native tools and their descriptions, argument schemas, error texts, prompts I read, APIs (Lua, addon loading) |
| **user** | Only the user can see it | UI, localization, visuals, settings screens |

The lane decides who supplies the facts. In the **user** lane I cannot observe the thing at all, so I interview for what the report needs and record what I was told - the gaps get filled by the user or they stay gaps. In the **agent** lane the evidence is mine, and it is exact: what I called, and what came back.

## Steps

### 1. Say it in one line, and offer

The moment friction happens, say it in one line and offer to file it. The offer is the filter: friction that does not survive one question is not worth a file.

In the user lane the user opens the topic, so step 1 is asking for the one-line version rather than offering one.

*Done when:* the friction is on the table in one line, and the user said yes - or asked for the report directly.

### 2. Look for the report that already exists

List `docs/agent-reports/` and grep it for the tool name, the surface and the symptom. A second sighting of the same friction is **the same report**: append a witness and stop. Two files about one problem split the evidence and get read as two complaints instead of one pattern.

*Done when:* either an existing report grew a witness line, or the folder is known not to hold it.

### 3. Take the number

`AR-NNNN` - the next free number after the highest in the folder, taken from the listing made in step 2. Check the filename is free immediately before writing; another session may be writing at the same moment.

*Done when:* the filename exists in nobody's folder but mine.

### 4. Write it

The template below. Every section filled; an empty **Unverified** is fine, an empty **Witnesses** is not.

Digging through the dASS source to pin the cause is **optional**. This is a report, not a patch, and a session spent outside the dASS repository should not turn into a code dive over one rough edge. When a claim comes from the source rather than from behaviour, say so in the report and mark it as traced.

*Done when:* the file is on disk with a lane, a severity, a repro and at least one witness.

### 5. Show the path

The path plus two or three lines of summary. The user will open the report; retelling it wastes the retelling.

*Done when:* the user has the path.

## The template

```markdown
# AR-0001 - <the friction in one line>

- **Status:** open | fixed | wontfix
- **Severity:** silent | visible | friction
- **Lane:** agent | user
- **Surface:** tool `fs-edit` | prompt | API | UI | localization | docs
- **First seen:** YYYY-MM-DD

## What I expected

The behaviour the tool's own description promises, in the model's reading of it.

## What happened

What actually came back - the call, the output, the file that ended up on disk.

## Why it costs

What the surprise costs per occurrence: steps, tokens, attention, or the correctness of the result.

## Repro

The smallest sequence that shows it. A call the maintainer can repeat.

## Witnesses

- YYYY-MM-DD - <session> - <what I was doing when it happened>

## What I did instead

The workaround. A workaround that costs three calls is the argument for the fix.

## Proposed fix

Options, cheapest first. A traced cause goes here, marked as traced.

## Unverified

What was inferred rather than observed, and what could not be checked from where I stood.
```

Status changes: `open` → `fixed` when the behaviour is gone, `wontfix` when the user decides against it. A `wontfix` keeps its reason in the report - it is what stops the next session from re-filing the same thing.

## Severity

| Level | What it means |
|---|---|
| **silent** | The tool lied and the result *looks right*. Nobody notices until much later. The worst class. |
| **visible** | It failed loudly. I saw it, and worked around it. |
| **friction** | It works, but every occurrence costs steps, tokens or attention. |

## Not here

- A fix I can write myself, as an addon → `equip`.
- A feat worth remembering → `record-achievement`.
- A design idea the user wants to pursue on their own terms → `docs/ideas/`.
