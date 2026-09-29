---
name: equip
description: Find and forge new dASS addons (skills, sub-agents, scriptable tools, LLT prompt parts) out of a finished session, so the next session needs less manual work. Use when a session is wrapping up, when the user asks what could be improved, when the same manual ritual keeps repeating, or when a missing capability blocked the work.
---

# Equip (dASS)

A finished session is the only honest evidence about what this agent lacks. **Equip** turns that evidence into addons, and into a **loadout** the next session starts from.

Two phases, in this order: **propose, then forge.** The proposal phase writes nothing to disk - the user picks first.

## Steps

### 1. Name the session

One line: which session, what the work was, when it ended. Default to the current session unless the user names another.

*Done when:* the line is on screen before a single candidate is offered.

### 2. Read the signals

Walk the session against every signal below. A signal that fires yields a **candidate** - one addon-shaped gap.

| Signal | What it usually wants |
|---|---|
| A tool call failed, or took a second attempt with a fix | A tool that does the thing in one call |
| The same command or query, twice in one session | A tool that packages the pair |
| The same external fact fetched twice | A tool that wraps or caches the lookup |
| A multi-step manual ritual | One tool exposing the whole ritual |
| "I have no way to do this" - the work stopped | A missing capability, the loudest signal there is |
| A source that could not be reached at all | A tool that reaches it |
| The same output shape hand-assembled again | A template, or a prompt part |

*Done when:* **every** signal has been walked, and each one either produced a candidate or was dismissed in one line. A session that fires nothing gets said out loud - "nothing worth forging here" is a valid answer, and a useful one.

### 3. Kill the duplicates

For each candidate, look before forging: `addon-search` first - it searches addon names, tags and descriptions - then a grep over the addon sources and over the loadout notes, where a refused candidate leaves its reason. A candidate that already exists in any form is dead, and its death is recorded in the note too.

*Done when:* every surviving candidate carries one line naming what was searched and what came back.

### 4. Rank by severity

Severity is **frequency × cost per occurrence** - sessions, context, tokens, wall time. Not novelty, not elegance. A tool that saves one call a week loses to a tool that saves one call an hour.

*Done when:* the list is ordered and every candidate carries its one-line justification.

### 5. Present, then stop

Per candidate: kind, proposed name, what stops being manual, the signal that revealed it, the cheapest language that can do it, and the cost to forge (S / M / L).

Then stop. The user picks. This is the phase boundary, and it is the point of the whole skill: the proposal is worth more than the forging, so it does not get rushed.

*Done when:* the list is in the user's hands and nothing has been created.

### 6. Forge the picks

One addon at a time. Locations and file shapes are in [FORMATS.md](FORMATS.md); the shape of the tool itself is the `tool-authoring` skill.

Language order for a scriptable tool: **Lua first** - it is the only engine every build has, and it depends least on the machine it lands on. **Python second**, when the job needs a library the Lua APIs do not reach and the tool can live with being desktop-only and text-only. **C# script last** - its API surface costs more than the tool usually saves.

A forged tool names its `behaviours` truthfully - those flags are what the approval policy gates it by - and `tool-authoring` carries the rest of the contract.

*Done when, per addon:* the file sits in the right folder of an addon source, its frontmatter name matches its file or folder name, and its keys and behaviours are the ones its own engine documents (see `tool-authoring`).

### 7. Write the note

Now, **after** the forging. One file - `.agents/skills/equip/loadout/YYYY-MM-DD_<slug>.md`, next to the skill that reads it - carrying the reasoning: which signals fired, which candidates they produced, what was forged, and why the refusals were refused. A refusal is a result; its reason is what keeps the same idea from coming back next session, and step 3 reads these notes before the next forging.

The date comes from `time-get`. The book of achievements is not written here - a forged addon is a decent candidate for `record-achievement`, and that call is the user's.

*Done when:* the note covers **every** candidate from step 5 - forged, refused and deferred alike.

## Not equipment

**Equip** adds equipment the agent carries into the next run. Its neighbours own the rest:

- Code architecture - deepening modules, moving seams - is `improve-codebase-architecture`.
- A dASS tool, prompt or API that contradicts its own contract is a report, not equipment: `dass-report`.
- The narrative of a feat - what was hard, how it was cracked - is `record-achievement`.
- Steering files and automated checks sit outside equip by decision, not by accident.
