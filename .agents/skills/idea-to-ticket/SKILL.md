---
name: idea-to-ticket
description: Record an idea and the agent's proposal for it as one small ticket under docs/ideas/<scope>/. Use when the user shares a feature idea, a design preference or a "would be nice" and a proposal comes back to it, when the user asks to write the idea down / file it / remember it / turn it into a ticket, or when a decision from a discussion deserves to outlive the chat. This is an idea-and-proposal store, not a plan, a spec or a wayfinder map.
argument-hint: "[scope:%Topic folder under docs/ideas/ to file into]"
---

# Idea to ticket (dASS)

A ticket here is a **two-voice record**: what the user wanted, and what the agent proposed back. It exists so an idea and the thinking around it outlive the chat window — not to schedule work.

## When it is active

- The user shares an idea, and a proposal comes back to it.
- The user says "write this down", "file it", "make a ticket", "remember this", "put it in docs/ideas".
- A decision, a correction or a refused alternative appeared in the discussion and is worth keeping.
- The user invokes `/skill:idea-to-ticket`.

## What this is not

| Not | Because |
|---|---|
| A plan | No stages, no ticket graph, no per-stage checklist, no `issues/` subfolder. One file, one idea. |
| A spec | It records a discussion, not a locked design with numbered requirements. |
| A wayfinder map | No research/prototype/grilling children, no frontier, no claim-and-resolve cycle. |
| A bug report | Something behaving unlike its description goes to `dass-report`. |

If the idea turns out to be genuinely big, say so and let the user pick the heavier vehicle. Do not quietly grow this format into one.

## Where it goes

`docs/ideas/<scope>/<idea-slug>.md` — one idea per file, kebab-cased slug, no numbers.

`docs/ideas/` is the idea and proposal store and sits deliberately outside the issue tracker: nothing is dispatched
from here, and no frontier scans it.

- `<scope>` groups ideas by topic (`web-ui`, `lua-api`, ...), taken from the skill argument or from the conversation.
  Create the folder on first use; a scope is a topic, never a plan.
- The header borrows the tracker's vocabulary (`docs/issues/README.md` is its authority) so an idea can be promoted
  into `docs/issues/` without being reworded.

## The format

```markdown
# <Short title of the outcome, not of the topic>

Status: ready-for-agent
Type: task
Blocked by:

## Requested

- <the user's idea, point by point, in requirement language>
- <each constraint or preference they stated>
- <the open question they named, if they named one>

## Proposed design

- **<Claim.>** <Why, and what it replaces or corrects.>
- <what is accepted from the request as it stands>
- <what is added, corrected or refused, and on what grounds>

## Not in scope

- <the tempting adjacent thing, and why it is not being taken>

## Comments
```

Optional sections, when they carry weight: `## Prerequisite` (something that must exist first), `## Alternatives considered` (when a refused option needs its reasons kept).

`Type: task` for work-shaped ideas. `Status:` uses the tracker's vocabulary: `open` while something blocks it or the design is unsettled, `ready-for-agent` when it is settled and nothing does, `resolved` once done — plus the triage strings. `Blocked by:` names another idea when one genuinely gates the other, and stays empty when nothing does.

## Two sections, two voices

Keeping them apart is the whole point of the file.

- **`## Requested` is the user's voice, paraphrased into requirements.** Their asks, their constraints, the shape they proposed, the question they left open. Requirements language, never a transcript.
- **`## Proposed design` is the agent's voice.** What is accepted as it stands, what is corrected, what is added, what is refused and on what grounds. An agent that only agrees is not doing this job: the corrections are the value, and so are the refusals — they stop the same argument from being had twice.

Write the user's side only from what the user actually said. Do not answer their questions on their behalf, and do not upgrade a half-formed idea into a decision they never made.

## House style

- **English, neutral spec prose.** No chat quotes, no colloquialisms, no swearing, no first person, no "as we discussed".
- **No file paths, no code.** They go stale; name types and concepts instead. Exception: a shape that encodes a decision more precisely than prose — a marker contract, a state machine, a type sketch — trimmed to the decision-rich part.
- **Small.** Bullets, one claim per line. A ticket that needs a table of contents is the wrong vehicle.
- Prefer the project's domain vocabulary over invented names.

## What bites

- **Blending the voices** — the requested and the proposed sections answering each other line for line, so the reader cannot tell the idea from the opinion about it.
- **A plan in disguise** — stages, cross-ticket dependencies, checklists, a nested `issues/` folder.
- **Quoting the conversation** — the user's phrasing, in their language, verbatim. Paraphrase into requirements.
- **Inventing a `Status:` string** that is not in the tracker's vocabulary.
- **Filing the proposal while dropping what it refused** — the refusals are half the record.

## Not here

- Deciding whether an idea deserves work at all → `grilling`.
- Slicing an approved design into implementable tickets → `to-tickets`.
- A bug about dASS itself → `dass-report`.
- Turning a finished session into reusable addons → `equip`.
