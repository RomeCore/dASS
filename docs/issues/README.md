# Issue tracker: Local Markdown, under `docs/issues/`

This file is the **tracker configuration** for this repo, and also the **front page** of the tracker itself. It is the authority read by the engineering skills (`wayfinder`, `to-spec`, `to-tickets`, `triage`, `implement-spec`, `domain-modeling`) — there is no `docs/agents/` in this repo, so everything those skills need to know lives here.

Issues, maps and specs for this repo live as markdown files in **`docs/issues/`**. We do not use GitHub Issues for this workflow, even though the repo has a GitHub remote.

## Conventions

- One effort per directory: `docs/issues/<effort-slug>/`
- The spec for an effort is `docs/issues/<effort-slug>/spec.md`
- Implementation issues are one file per ticket at `docs/issues/<effort-slug>/issues/<NN>-<slug>.md`, numbered from `01`, never a single combined tickets file
- Triage state is recorded as a `Status:` line near the top of each issue file (see [Triage labels](#triage-labels))
- Comments and conversation history append to the bottom of the file under a `## Comments` heading
- The directory is plain markdown and safe to commit; keep slugs short and kebab-cased

### Ticket file skeleton

```markdown
# <Ticket title>

Status: open
Type: grilling
Blocked by:

## Question

<the decision or investigation this ticket resolves>

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
```

`Type:` is one of `research`, `prototype`, `grilling`, `task` (see the `wayfinder` skill). `Status:` is one of `open`, `claimed`, `resolved`, plus the triage role strings when `triage` is involved. `Blocked by:` lists ticket numbers, comma-separated, or stays empty.

## When a skill says "publish to the issue tracker"

Create a new file under `docs/issues/<effort-slug>/` (creating the directory if needed).

## When a skill says "fetch the relevant ticket"

Read the file at the referenced path. The user will normally pass the path or the issue number directly.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a file with one **child** file per ticket.

- **Map**: `docs/issues/<effort>/map.md` — holds the Destination / Notes / Decisions-so-far / Not-yet-specified / Out-of-scope body. Label its title `wayfinder:map` in spirit: the file name itself is the marker, and there is no label system for maps.
- **Child ticket**: `docs/issues/<effort>/issues/NN-<slug>.md`, numbered from `01`, with the question in the body. A `Type:` line records the ticket type (`research`/`prototype`/`grilling`/`task`); a `Status:` line records `claimed`/`resolved` (or `open`).
- **Blocking**: a `Blocked by: NN, NN` line near the top. A ticket is unblocked when every file it lists is `resolved`.
- **Frontier**: scan `docs/issues/<effort>/issues/` for files that are open, unblocked and unclaimed; first by number wins.
- **Claim**: set `Status: claimed` and save before any work. A claimed ticket is not takeable by another session.
- **Resolve**: append the answer under an `## Answer` heading, set `Status: resolved`, then append a context pointer (gist + link) to the map's Decisions-so-far in `map.md`.
- **Refer by name**: in everything a human reads, refer to a ticket or map by its **title**, not by its bare number or slug. The number/path rides inside the name as a link, never stands in for it.

A ticket is sized to one agent session (~100K tokens). A HITL ticket only resolves through a live exchange with the human; never answer the human's side of it on their behalf.

## Triage labels

The skills speak in terms of five canonical triage roles. This table maps those roles to the actual `Status:` strings used in this tracker.

| Role in the skills | String in this tracker | Meaning |
| --- | --- | --- |
| `needs-triage` | `needs-triage` | Maintainer needs to evaluate this issue |
| `needs-info` | `needs-info` | Waiting on reporter for more information |
| `ready-for-agent` | `ready-for-agent` | Fully specified, ready for an AFK agent |
| `ready-for-human` | `ready-for-human` | Requires human implementation |
| `wontfix` | `wontfix` | Will not be actioned |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding string from this table. Edit the right-hand column to match whatever vocabulary you actually use.

## Domain docs

Single-context layout:

- `GLOSSARY.md` at the repo root — the ubiquitous-language glossary
- `docs/adr/` — architecture decision records, one file per ADR

Read the glossary before naming domain concepts in a spec or ticket, and add to it when a new term is settled. Nothing here exists yet — create the file or folder on first use.

## Agent skills pointer

`AGENTS.md` carries a short `## Agent skills` block that points here, so the engineering skills can discover this file without guessing.
