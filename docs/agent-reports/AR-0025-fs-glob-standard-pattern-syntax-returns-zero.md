# AR-0025 - `fs-glob` answers "No matches found" for `?`, `[abc]`, `{a,b}` and absolute patterns, the same way it answers for a file that does not exist

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `fs-glob`
- **First seen:** 2026-10-26

## What I expected

The description opens with "Finds files and directories matching a glob pattern", then lists exactly two deviations:

```
Differences from bash's `glob`:
- NO one-character matching like [Aa]
- NO one-word matching like {abc,def}
```

The plain reading of a closed list of two deviations is that everything else bash accepts, the tool accepts. The description also names no `?` exclusion - and the deviation text even calls `[Aa]` "one-character matching", which is the name of the `?` wildcard, so a model reading this can reasonably conclude `?` *is* supported.

There is a second, unstated contract in play: this is a file-finding tool given to an agent that works in bash and in other agent runtimes. Claude Code's `Glob` - which is what an agent's habit is trained on - runs `ripgrep --files --glob <pattern>` (traced: `claude-code/src/utils/glob.ts` in the reference checkout), i.e. the full ripgrep glob dialect with `?`, `[...]`, `{a,b}` and absolute patterns all working. Its prompt promises `**/*.js` and `src/**/*.ts` and lists no limitation at all.

So my expectation was: either the pattern works, or the tool tells me it cannot be honoured. What it must not do is answer "no such files", because that is a statement about the file system, not about the pattern.

## What happened

Six calls from the workspace root, default arguments except `limit`. Every one of them is a control/variant pair aimed at the same file set - so the difference in output can only come from the syntax.

| Pattern | Result |
|---|---|
| `**/FileUtils.cs` | **1** - `src\LLMDesktopAssistant\Utils\Files\FileUtils.cs` |
| `**/FileUtil?.cs` | **0** - `No matches found` |
| `**/[Ff]ileUtils.cs` | **0** - `No matches found` |
| `**/*.loc` | **3** - `.integrai\_migration\Old-iv.loc`, `.integrai\_migration\Old-ru-RU.loc`, `src\LLMDesktopAssistant\Localization\Resources\iv\addon.loc` |
| `**/*.lo[c]` | **0** - `No matches found` |
| `**/*.{cs,loc}` | **0** - `No matches found` |

And absolute patterns, which `Glob` in other runtimes accepts and which an agent reaches for by habit:

| Pattern | Result |
|---|---|
| `<absolute path to the repo>/src/**/*.csproj` | **0** - `No matches found` (there are 6+ `.csproj` files under `src/`) |

The full body of the failing call is:

```
[GLOB RESULTS]
Pattern: **/FileUtil?.cs
Path: .
Found: 0 item(s)

No matches found.
```

Nothing in the output distinguishes "your pattern used syntax I do not implement" from "the file is not there". The tool reports `ToolExecutionResult.Succeeded` - this is not a caught error text, it is the success path.

Case sensitivity is *not* a factor here: `**/fileutils.cs` (all lowercase) correctly returned `FileUtils.cs`, which is what I expected on a Windows file system.

## Why it costs

- **It is the silent class of failure.** "No matches found" is indistinguishable from a true negative, so the reading it produces is a false fact about the codebase: *there is no file named `X`, this project has no `.loc` resources, the tool I was about to reuse does not exist here*. Nothing retries, nothing warns, and the wrong fact goes into the plan.
- **The rejected syntax is the syntax an agent actually uses.** `**/*.{ts,tsx}`, `**/[Ff]oo*`, `**/?s-read*` are not exotic - they are the everyday vocabulary of a shell and of every other agent runtime's glob tool. The probability of hitting this is not low, it is the default.
- **Each occurrence costs a guessing round.** Recovering means retrying with a narrower dialect and then post-filtering by hand: split the brace set into N calls, drop the character classes and hope the name is unique enough, turn an absolute path into `path` + relative pattern. That is 2-4 extra calls per search, on a tool whose whole job is to save calls.
- **The description actively misleads.** By naming `[Aa]` "one-character matching" it pushes the reader to infer that the *other* one-character wildcard (`?`) is available, and that inference is wrong. A description that is expensive to misread is worse than a description that is thin.

## Repro

```
1. `fs-glob` with pattern `**/FileUtils.cs`            -> 1 result
2. `fs-glob` with pattern `**/FileUtil?.cs`            -> 0 results, "No matches found"
3. `fs-glob` with pattern `**/[Ff]ileUtils.cs`         -> 0 results
4. `fs-glob` with pattern `**/*.loc`                   -> 3 results
5. `fs-glob` with pattern `**/*.lo[c]`                 -> 0 results
6. `fs-glob` with pattern `**/*.{cs,loc}`              -> 0 results
7. `fs-glob` with an absolute pattern pointing at the same tree, e.g. `<drive>/<repo>/src/**/*.csproj`
                                                       -> 0 results (the files exist)
```

All seven calls succeed. Only the syntax of the pattern differs between the working and the empty result.

## Witnesses

- **2026-10-26** - session reviewing `fs-glob` on the user's request. The mismatch was also reported from outside that session: the user described the tool's dialect as differing sharply from bash and from Claude Code's Glob, which is the same behaviour this report records.

## What I did instead

- Split brace sets into one call per extension: `**/*.cs` and `**/*.loc` separately.
- Dropped character classes entirely and relied on literal names (`**/FileUtils.cs`), accepting that I cannot search case-variant names in one call.
- For absolute paths: peeled the static prefix into `path` and sent the rest as a relative pattern.
- For "find a tool module by prefix" I gave up on the glob and used `fs-grep` with a regex over the filesystem, which reads every candidate file instead of listing names.

## Proposed fix

1. **Refuse what cannot be honoured (cheapest, closes the silent class).** One check before matching: if the pattern contains `?`, `[`, `{`, `}` or starts with a drive/root prefix, return `Failed` with the reason and the working alternative ("`?` and `[...]` are not supported - use `*`; braces are not supported - send one call per alternative"). Four lines, no behaviour change for patterns that already work, and the failure becomes loud instead of being cached as a fact about the codebase.
2. **Pre-expand braces and translate the pattern to a matcher that understands it.** Brace expansion into N include patterns is a string operation; `?` and `[abc]` are a straight translation into the regex form of the same segment, with the regex applied to the relative path instead of feeding `Matcher`. This makes the tool accept what its description implies, without a new engine.
3. **Handle absolute patterns by splitting, not by rejecting.** Take the static prefix up to the first wildcard as the search root and the remainder as the relative pattern. Traced reference: `extractGlobBaseDirectory` in the Claude Code checkout does exactly this, for the same reason (the matcher only takes relative patterns).
4. **Longer term, own the dialect.** A bash-compatible matcher over a single `EnumerateFileSystemEntries` walk, results sorted by modification time, output as bare relative paths. Shelling out to `ripgrep` - the reference implementation's choice - is not available here: the core project is portable (`.Android`, `.Browser`/WASM) and the tool must run in-process. A shared matcher would also serve `fs-grep`, which currently accepts no include pattern at all, so "search only in `**/*.cs`" is inexpressible.
5. **Fix the wording either way.** "NO one-character matching like [Aa]" mislabels a character class as the one-character wildcard; and `?` has to be named explicitly if it stays unsupported.

## Unverified

- Traced, not measured: the engine is `Microsoft.Extensions.FileSystemGlobbing` (`using` at the top of `FilesystemGlobToolModule.cs`, `new Matcher()` + `AddInclude(pattern)`), whose documented dialect is `*`, `**` and literals. I did not read its parser to confirm that `?`/`[`/`{` are treated as literal characters rather than being rejected - the observation (0 results, `Succeeded`) stands either way, but the exact mechanism inside the package is inferred.
- Not exercised: `**` alone, `!` negation, backslash escapes, a `path` argument with a trailing separator, UNC paths, and whether `path` itself accepts an absolute value (that path uses a different code path - `_fileAccess.CheckedAccessPath` - so it may well work where the pattern does not).
- Claude Code's behaviour is read from that project's source (`src/utils/glob.ts`, `src/tools/GlobTool/prompt.ts`) in a local checkout kept outside this repository, not executed. The checkout's location is not recorded here on purpose.
