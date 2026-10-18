---
name: commit-message
disable-model-invocation: true
description: Name a commit from the repository diff. When the diff is already in the message (`/commit-message` injects it), use it as is; otherwise call the `git-full-diff` tool as the first action of the turn, before replying - it already returns the diff, the stats, the scope counts and the recent commit history, so no separate `git log` is needed. Use when the user asks for a commit name or message, hands over a diff to be named, or is about to commit. Pass `auto` (`/commit-message auto`) to also create the commit instead of stopping at the message.
---

# Naming a commit from the diff

The product is a short list of candidate names plus one ready-to-paste message. The diff is the truth; the user's summary is a hint.

## The argument

Read one optional argument from the invoking message (`/commit-message <arg>`):

- **no argument** - name only: produce the candidates and the ready-to-paste message, then stop. Committing stays a separate step the user asks for.
- **`auto`** - name *and* commit: run steps 1-5, then create the commit(s) yourself (step 6). The argument *is* the user's instruction to commit - do not ask for confirmation again.

Any other argument is treated as absent: name only.

## Steps

### 1. Get the diff

**If the diff is already in the message** - `/commit-message` injects it as parts titled `Diff`, `Status` and `Summary` - do not call anything: it is in view. **Otherwise** call `git-full-diff` as the first action of the turn, in the same turn as the reply. Either way it returns the same material in one call: the tracked diff (staged and unstaged, `git diff HEAD`), the untracked files folded into the stats, the scope counts and the recent history.

Do not run `git status`, `git diff` or `git log` yourself - unless the injected content says the diff is not included (a `Status` part instead of a `Diff` part). Then read only what the naming actually needs: `git diff HEAD -- <path>` for a modified file, `fs-read_entry` for a new one, file by file.

*Done when:* the diff - or the status plus the files it points at - its stats, the recent commits and the scope counts are in view.

### 2. Find the intent

One commit names one **intent**: a new capability, a fixed behaviour, a restructuring that leaves behaviour intact. The intent is set by the diff, not by the file count - a 13-file rename is `refactor`, a one-line new flag is `feat`.

*Done when:* the dominant intent is named, and every change belonging to a *different* intent is named too.

### 3. Name it

`type(scope): subject`, per [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/). The type names the **intent**, not the files touched or the folder they sit in - a one-line new flag is `feat`, a 13-file rename is `refactor`.

| Type | Use it when the commit... | SemVer |
|---|---|---|
| `feat` | adds a new capability the user or agent gets | MINOR |
| `fix` | patches a bug | PATCH |
| `refactor` | restructures code without changing behaviour | - |
| `perf` | makes something faster or lighter | - |
| `docs` | touches documentation only | - |
| `test` | touches tests only | - |
| `style` | formats **code** - whitespace, ordering, quotes - no behaviour, **never UI or layout** | - |
| `chore` | maintenance: dependencies, tooling, config - no production code | - |
| `build` | build system or packaging | - |
| `ci` | CI configuration | - |
| `revert` | undoes an earlier commit | - |
| `agent` | agentic infrastructure: addon packs, skills, tools, prompts, anything under `.agents/` *(repo-specific)* | - |
| `update` | catch-all refresh of docs/config that fits no single type above *(repo-specific)* | - |

- **feat vs refactor.** Both change code; the split is *new observable behaviour*. Adds it - `feat`. Keeps it identical while reshaping the internals - `refactor`. When a commit does both, pick the one that dominates the diff and answers the reviewer's likely question *why*: a feature that happens to move files stays `feat`; a reshuffle whose only visible effect is "same behaviour, different code" is `refactor`. Genuinely independent halves are a split, not a coin flip - see step 5.
- **UI is not `style`.** A layout, spacing, colour, animation or visibility change *is* a behaviour change: `feat` for a new look, `fix` for a broken one - and `agent` in this repo when the surface belongs to the agent infrastructure. `style` never means "styling".
- **BREAKING CHANGE.** Append `!` after the type/scope (`feat(api)!: ...`) or add a `BREAKING CHANGE:` footer when an existing contract breaks. It rides on any type.
- Scopes drift - reuse the scope that area already carries (`tools`, `addons`, `prompts`, `lua`, `mvvm`, `ui`, `chat`, `agents`, `agentic-infra`, ...) before inventing one. Read it from the **scope table** of the output (`| scope | count |`, collected over the last 500 commits), not from the 25-line history fence - the fence is a sample, the table is the vocabulary - and not from a fresh `git log`. A scope missing from the table is not forbidden: the table records what exists, it does not prescribe what is allowed.
- Subject: imperative (`add`, `fix`, `decouple`), lowercase after the colon, no trailing period, unique enough that it could not be the subject of any other commit in the log.

*Done when:* the type is one of the rows above and its intent matches the diff, and the scope is either seen in recent history or explicitly justified as new.

### 4. Produce the output

- **Candidate names** - always a handful (2-4), the recommended one first. Variants must differ in type, scope or phrasing, not in punctuation; no filler.
- **One detailed message** - the recommended name as a full commit message: subject, then a body when the diff carries more than one change or the *why* is invisible in the code (a bug's cause, a constraint, a rejected alternative). Body bullets are imperative, one per change. One-line diffs get no body.

*Done when:* candidate names and one complete message are on screen, with a one-line rationale for the pick.

### 5. Flag a splittable diff

When the diff holds independent changes, add a **How to split** section on top - one message per commit, each listing the files or hunks it takes. Close with a one-line hint that committing is a separate step - the user can ask me to commit it.

*Done when:* either the diff is declared a single intent, or every independent change has its own proposed commit.

### 6. Commit it (only with `auto`)

Without the `auto` argument, stop at step 5 - the message is the whole product. With it, the message is already written, so make the commit:

- Stage exactly the paths the diff listed, tracked and untracked alike (`git add -A` when the diff *is* the whole worktree); leave everything else in the worktree untouched.
- Commit the subject with `git commit -m`, adding the body as further `-m` paragraphs when step 4 produced one.
- A splittable diff becomes one commit per proposed split, in the listed order - never one squashed commit.
- Never push. Report the created commit hash(es).

*Done when:* `git log` shows the new commit(s) with the exact recommended subjects.

## Pitfalls

- Naming from the user's summary instead of the diff - the summary is stale by the time it reaches you.
- A body bullet that changes behaviour belongs in its own commit, not buried under another intent.
- A scope invented for a one-off - check recent history first.
- Running `git log`, `git status` or `git diff` by hand - the injected diff (or `git-full-diff`) already carries the history, the diff and the stats. The one exception is a brief injection, which says so and tells you to read the files yourself.
- `style` for a UI or layout change - `style` is code formatting only; a visual change is `feat`, `fix` or `agent`.
- `feat` for a commit whose only lasting effect is that the same behaviour now lives in different code - that is `refactor`.
- Committing without `auto` - no argument means name only; the commit is the user's call, not mine.
- Asking for confirmation again under `auto` - the argument *is* the confirmation; name it and commit it.
