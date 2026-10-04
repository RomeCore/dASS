---
name: commit-message
disable-model-invocation: true
description: Name a commit from the repository diff. Call the `git-full-diff` tool immediately as the first action of the turn - before replying - then name the change. Use when the user asks for a commit name or message, hands over a diff to be named, or is about to commit.
---

# Naming a commit from the diff

The product is a short list of candidate names plus one ready-to-paste message. The diff is the truth; the user's summary is a hint.

## Steps

### 1. Call `git-full-diff` immediately

First action of the turn, in the same turn as the reply - not `git status`, not `git diff`. It folds untracked files into the diff and the stats, and it costs one round trip, so spend none on a preamble.

*Done when:* the diff and its stats are in view.

### 2. Find the intent

One commit names one **intent**: a new capability, a fixed behaviour, a restructuring that leaves behaviour intact. The intent is set by the diff, not by the file count - a 13-file rename is `refactor`, a one-line new flag is `feat`.

*Done when:* the dominant intent is named, and every change belonging to a *different* intent is named too.

### 3. Name it

`type(scope): subject`.

- Types are fixed: `feat`, `fix`, `refactor`, `docs`, `style`, `perf`, `test`, `chore`, `update`.
- Scopes drift - read `git log --oneline -30` and reuse the scope that area already carries (`tools`, `addons`, `prompts`, `lua`, `mvvm`, `ui`, `chat`, `agents`, ...) before inventing one.
- Subject: imperative (`add`, `fix`, `decouple`), lowercase after the colon, no trailing period, unique enough that it could not be the subject of any other commit in the log.

*Done when:* the type comes from the fixed list and the scope is either seen in recent history or explicitly justified as new.

### 4. Produce the output

- **Candidate names** - always a handful (2-4), the recommended one first. Variants must differ in type, scope or phrasing, not in punctuation; no filler.
- **One detailed message** - the recommended name as a full commit message: subject, then a body when the diff carries more than one change or the *why* is invisible in the code (a bug's cause, a constraint, a rejected alternative). Body bullets are imperative, one per change. One-line diffs get no body.

*Done when:* candidate names and one complete message are on screen, with a one-line rationale for the pick.

### 5. Flag a splittable diff

When the diff holds independent changes, add a **How to split** section on top - one message per commit, each listing the files or hunks it takes. Close with a one-line hint that committing is a separate step - the user can ask me to commit it.

*Done when:* either the diff is declared a single intent, or every independent change has its own proposed commit.

## Pitfalls

- Naming from the user's summary instead of the diff - the summary is stale by the time it reaches you.
- A body bullet that changes behaviour belongs in its own commit, not buried under another intent.
- A scope invented for a one-off - check recent history first.
