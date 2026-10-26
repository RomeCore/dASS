# AR-0026 - `fs-glob` with `showHidden=false` filters only the last path segment, so `.git/config` and everything else under a hidden directory is returned

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `fs-glob`
- **First seen:** 2026-10-26

## What I expected

The argument says:

```
showHidden: Whether to show hidden files and directories (those starting with dot).
default: false
```

The plain reading is about *paths*: with the default, no entry that lives in a dot-prefixed file or directory should appear in the results. `.gitignore` is hidden, and so is `.git/config` - the second one because its parent is hidden, which is what "hidden files and directories" means to any shell.

## What happened

The filter is applied to the leaf name only, so a hidden *ancestor* is invisible to it.

Control calls, both with the default `showHidden=false`:

`fs-glob` with `**/config` - a pattern that never mentions `.git`:

```
[GLOB RESULTS]
Pattern: **/config
Path: .
Found: 1 item(s)

[FILE] .git\config (534 B, 23 lines, 2026-10-17 23:28)
```

`fs-glob` with `.git/**/*`, `limit=5`:

```
[FILE] .git\COMMIT_EDITMSG (790 B, 9 lines, 2026-10-26 16:50)
[FILE] .git\config (534 B, 23 lines, 2026-10-17 23:28)
[FILE] .git\config.worktree (83 B, 5 lines, 2026-08-02 16:27)
[FILE] .git\description (73 B, 1 lines, 2026-02-24 11:03)
[FILE] .git\FETCH_HEAD (210 B, 2 lines, 2026-10-26 17:21)
```

The second call names `.git` explicitly, so on its own it could be argued as "you asked for it". The first does not, and still returns `.git\config` - that is the finding. The directory-level filter has the same shape: `**` at the top of the tree does exclude `.git`, `.github`, `.agents`, `.vs` and `.llmassist`, because for a *directory result* the dot is in the leaf name; it is the walk *through* a hidden directory that is never filtered.

The result is a half-hidden tree: `.gitignore` is excluded (dot leaf), `.git/config` is not (dot ancestor). Same directory, same call, opposite answers.

## Why it costs

- **The filtered result looks filtered.** The flag says the hidden noise is gone, so the returned list is trusted as "the real matches". A search for `**/config`, `**/HEAD`, `**/index`, `**/description` or `**/settings.json` on a project with a `.git`, `.vs`, `.idea` or `.vscode` will surface machine state as if it were project content, and nothing marks it as such.
- **It pollutes the answer with files the agent must not read.** `.git/config` plus `.git/COMMIT_EDITMSG` are exactly the kind of file an agent will open next (they look like configuration and like project data), and opening them costs a call and a confirmation prompt on a file nobody wanted.
- **It is expensive in the only place the flag could have paid.** The walk still traverses `.git/objects`, `.vs`, `node_modules/.cache`. On this repository `.git` alone is thousands of entries, and each one is stat-ed (`FileUtils.GetFileMetrics` reads the first 8 KB and, for text files, counts every line to render the trailing `(size, lines, date)`). The flag reads as "do not pay for hidden trees", and it does not.

## Repro

```
1. fs-glob, pattern `**/config`, showHidden omitted (false)
   -> `.git\config` is in the results.

2. fs-glob, pattern `.git/**/*`, showHidden omitted (false)
   -> five files from `.git` are in the results.

3. Control: fs-glob, pattern `**`, directories=true
   -> `.git`, `.github`, `.agents`, `.vs`, `.llmassist` are correctly absent,
      which is what makes the inconsistency visible: the same dot rule holds
      for a result and not for a parent.
```

## Witnesses

- **2026-10-26** - session reviewing `fs-glob` at the user's request ("глянь тул для glob-поиска файлов"). Found while checking whether the tool hides the junk it walks; the user's verdict on the tool ("этот глоб сам по себе говно, разительно отличается от того, что в линуксе и в Claude Code") came before the probe and was about the dialect, not about this, so this one is my observation only.

## What I did instead

Nothing clever: I stopped trusting the flag and read every result line for a dot-prefixed segment before acting on it, and then re-ran the search against an explicit subdirectory (`path=src`) whenever a `.git` or `.vs` entry showed up. That is manual filtering of a flag that exists to do exactly that filtering.

## Proposed fix

1. **Filter on every segment of the relative path** (`Path.GetRelativePath(fullPath, filePath)` split on separators, any segment starting with `.`), not on `Path.GetFileName`. Same for the directory branch. This is the literal reading of the argument and a few lines of change.
2. **Separate the flag from the junk.** The real cost is not "hidden" but "the tree nobody wants scanned" - `bin`, `obj`, `.git`, `node_modules`, `.vs`, `TestResults`. An unconditional skip-list (or a `"exclude"` argument) serves both this report and the traversal cost; `showHidden` can then mean exactly what it says without having to be a performance knob.
3. **Say what the flag does today** if the fix is deferred: "hidden" currently means "the last segment starts with a dot".

## Unverified

- Traced, not measured: the filter is `Path.GetFileName(filePath).StartsWith(".")` in the file branch and `Path.GetFileName(dir).StartsWith(".")` in the directory branch of `FilesystemGlobToolModule.cs`. I read the filter, but not the `Microsoft.Extensions.FileSystemGlobbing` side, so I did not check whether that package has an option that would hide dot directories during the walk itself.
- Not exercised: `showHidden=true` (assumed to simply disable the check), a hidden file directly inside a visible directory (`src/.editorconfig` - the leaf check should catch it), and a hidden directory listed as a *result* with `directories=true` and an explicit pattern such as `.git/**` (expected to be filtered, since the dot is then in the leaf).
- Whether the walk costs what I claim on this repository was not measured with a timer; the `.git` and `bin/obj` sizes are an estimate from the file counts seen in earlier listings.
