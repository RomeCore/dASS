---
name: record-achievement
description: Records a completed piece of work as an achievement entry in .llmassist/achievements/ (the dASS "book of achievements"). Use when the user says "record an achievement", "log this win", "save this as an achievement", or asks to bookmark/remember a notable accomplishment, breakthrough, or hard-won result for posterity.
---

# Recording an achievement (dASS)

Turn **already finished** work into a self-contained entry in the dASS book of achievements.
The target reader is you in six months, another session, or another agent, **with no access to the chat**
in which all of this happened.

This is not a diary and not a changelog. It is a report of a feat: what happened, why it was hard,
how it was cracked, and what was left undone.

## Where and how to store it

- **Folder:** `<workdir>/.llmassist/achievements/` - exactly like that, with two `i`
  (the project once had the typo `achevements`; it is fixed - do not bring it back).
  If the folder does not exist, create it.
- **One file = one achievement**, name: `YYYY-MM-DD_ACH-NNNN_<slug>.md`
  - `YYYY-MM-DD` - the date of the entry (do not invent it, take it from `time-get`);
  - `ACH-NNNN` - 4 digits, **sequential** numbering;
  - `<slug>` - latin, kebab-case, 3–6 words describing the work.
- **ID:** look at what already exists (`fs-glob` over `.llmassist/achievements/*.md` or a folder listing),
  take **max + 1**. Numbers are never reused, never renumbered, never "compacted".
  If this achievement is already recorded - **edit the existing file**, do not create a duplicate.
- **Index:** `.llmassist/achievements/INDEX.md` - a table
  `| ID | Date | Title | Key result | File |`, rows sorted by ID.
  No file - create it; on a new entry - append a row at the end. Never break other people's rows.

## Entry structure

Mandatory sections (the rest is optional and depends on the scale of the work):

```markdown
# ACH-NNNN - <Short punchy title>

- **Date:** YYYY-MM-DD
- **Agent:** <who did it: chat agent/sub-agent, persona, mode>
- **User request:** "<verbatim wording, if there was one>"
- **Status:** ✦ DONE / ⚠ PARTIAL / ✖ FAILED

---

## TL;DR

3–6 bullets: what was done, with what, and where the result lives.

## Challenge

A "problem → consequence" table. What exactly got in the way: vendor restrictions,
dead links, missing documentation, someone else's code, a deadline, a vague task.

## How it was solved

An "approach → tool" table. The point is **reproducibility** - so the approach can be
repeated rather than merely admired.

## Artifacts

Paths, sizes, file names, commits, branches. What was created/changed/deleted.
If an artifact is temporary, say so explicitly.

## What did not work

MANDATORY. What failed, what could not be obtained, what is still a hack,
what risks were introduced. An empty section is a sign that the entry is lying.

## Why this is an achievement

2–5 bullets: which agent capabilities this demonstrates (web access, filesystem,
long chains, fact verification, working with someone else's code).

## User reaction

A verbatim quote of the user's reaction, if there was one. No quote - drop the section.
```

## Rules

1. **Only facts from this session.** Numbers, sizes, versions, paths - exactly as they were.
   Not sure about a number - do not write a number at all.
2. **Self-containment.** The reader did not see the chat: spell out abbreviations, give context.
3. **Honesty over beauty.** The "What did not work" section must not be empty if there were failures.
   An admitted pitfall is worth more than an invented triumph.
4. **Proportionality.** Small work - 20–30 lines, large work - as long as it needs. Do not inflate it.
5. **Verifiability.** Artifacts - by path; external sources - by link; claims from third-party docs -
   by quote, not by hearsay.
6. **Tone is free.** The agent's personality, profanity, jokes are fine if that is the style. But the facts
   underneath the style must stay exact.
7. **No secrets.** Tokens, passwords, private URLs, personal data - never.
   The `.llmassist/` folder may be committed or may simply leak.
8. **The entry language = the language of the user's request.**
9. **Dates come from `time-get` only.** A guessed date breaks sorting and trust.
10. **Do not edit past entries** without an explicit request (the exception is `INDEX.md`
    and broken links).

## Workflow

1. Figure out what exactly was achieved: the result, why it was hard, where it lives.
   If the work is trivial ("I ran the build") - tell the user straight and suggest not creating an entry;
   the decision is theirs, but an honest assessment is your job.
2. Get the current date (`time-get`).
3. Find the next free `ACH-NNNN`.
4. Assemble the entry using the template above.
5. Update `INDEX.md`.
6. Create/rename a folder only if it truly does not exist.
7. Show the user the **path to the file + a 2–3 line summary**. Do not retell the whole entry -
   they will open it anyway.

## Pre-delivery checklist

- [ ] The file lives in `.llmassist/achievements/`, the name follows the scheme, the ID is max + 1 and free.
- [ ] The date came from `time-get`, not from imagination.
- [ ] The header (ID, date, agent, request, status) is filled in.
- [ ] TL;DR, "How it was solved", "Artifacts" and "What did not work" are present.
- [ ] All facts and numbers are real and verifiable.
- [ ] There are no secrets or personal data.
- [ ] `INDEX.md` is updated (or created).
- [ ] The user was shown the path and a short summary.

## Reference

`2026-09-29_ACH-0001_cisco-air-lap1042n-docs-harvest.md` - the first entry, and this format was derived from it.
If you are unsure about style, open it and follow its structure.
