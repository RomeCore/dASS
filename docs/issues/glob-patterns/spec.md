# Glob patterns - specification

> Assembled from the design session on the `fs-glob` dialect. This is the implementable document; the reasoning is in `docs/agent-reports/AR-0025` and `AR-0026`, which it closes.
>
> Status of the implementation: `Utils/Patterns`, `Utils/Files`, `fs-glob`, `fs-grep` and the ignore policy of `fs-read_entry` are done as described here. Specifiers and the remaining migration steps are not.

## 1. Goal

One pattern layer that every consumer shares, so that a wildcard means the same thing in a file search, a name filter and a specifier rule.

Three things drive it:

- **One dialect, declared in full.** `fs-glob` promised bash-like behaviour and silently returned `No matches found` for `?`, `[abc]`, `{a,b}` and absolute patterns (AR-0025). A pattern the engine cannot honour must fail loudly at compile time, never look like an empty file system.
- **A resolvable prefix.** The leading segments of a filesystem pattern that need no matching become a start directory. That is an optimisation, the only place where an access check can see where the walk actually begins, and the reason a typo in `src` reports a missing directory instead of an empty result.
- **A flat mode.** Names, keys and tool ids are not paths. They need `*`, `?`, `[]` and `{}` without separators, and they are the majority of the consumers.

## 2. Scope and non-goals

In scope: pattern syntax, prefix extraction, the matching model, the ignore policy, errors, limits, the API, the walker, and the migration of the four dialects that existed before.

Out of scope: result ordering; `extglob`; POSIX classes (`[[:alpha:]]`); `.gitignore` semantics; `~` and environment expansion; backslash escaping; path-pattern specifiers (they arrive with generated analyzers).

## 3. Terms

| Term | Meaning |
|---|---|
| **unit** | The smallest construct: a literal, `*`, `?`, `[...]`, `{...}` or `**`. |
| **segment** | A run of units between separators. `**` occupies a whole segment. |
| **alternative** | One branch produced by brace expansion. |
| **prefix** | The leading segments that resolve without matching: literals and path operators. |
| **remainder** | The same pattern with its prefix removed. |

## 4. Two pattern types, no dialect enum

| Type | Input | Separators | `**` | Prefix |
|---|---|---|---|---|
| `GlobPattern` | `IReadOnlyList<string>` segments | structural | whole segments | yes |
| `GlobTextPattern` | `string` | ordinary characters | rejected | no |

The type **is** the dialect. There is no `GlobDialect` enum: an enum with two values that mirror the two types adds a third thing to keep consistent.

The command-tail operator `:*` is **not** a dialect and not a grammar construct. It is `GlobOptions.ArgumentTail`, applied by the compiler: a trailing literal `:` followed by a final `*` becomes `(?:$|[ \t].*)`. Without the flag, `:*` is an ordinary colon and a star, which is what a path (`C:*`) and a locale key need. It is rejected for `GlobPattern` (`OptionNotSupported`), because a path has no command tail.

## 5. Grammar

Parsed with RCParsing, one static `Parser` built once, exactly as `SpecifierParser` does. The grammar is dialect-free: it always produces the same unit list, and the type decides how the units are compiled.

```csharp
builder.CreateRule("literal")
	.NegativeLookahead(b => b.LiteralChoice('*', '?', '[', ']', '{', '}', '/', '\\'))
	.AnyChar()
	.Transform(v => new GlobLiteral(v.StartIndex, v.Text[0]));

// Inside a brace the comma and the closing brace terminate the alternative.
builder.CreateRule("brace-literal")
	.NegativeLookahead(b => b.LiteralChoice('*', '?', '[', ']', '{', '}', ',', '/', '\\'))
	.AnyChar()
	.Transform(v => new GlobLiteral(v.StartIndex, v.Text[0]));

builder.CreateRule("class")
	.Literal('[')
	.Optional(b => b.LiteralChoice('!', '^'))
	.Choice(b => b.Literal(']'), b => b.Rule("class-item"))
	.ZeroOrMore(b => b.Rule("class-item"))
	.Literal(']')
	.Transform(v => new GlobClass(v.StartIndex, v.Text[1..^1]));

builder.CreateRule("brace")
	.Literal('{')
	.OneOrMoreSeparated(b => b.Rule("brace-units"), b => b.Literal(','))
	.Literal('}')
	.Transform(v => new GlobBrace(v.StartIndex, CollectAlternatives(v)));
```

Rules: `separator` (`/` or `\`), `double-star`, `any-chars`, `any-char`, `class-item`, `class`, `open-bracket`, `open-brace`, `close-bracket`, `close-brace`, `brace-unit`, `brace-units`, `brace`, `unit`, `units`, and the main rule `pattern`.

Order inside `Choice` is semantics, not style:

- `brace` before the literal `{`: an unmatched `{` becomes a literal;
- `class` before the fallbacks: an unmatched `[` becomes a literal (the class rule fails as a whole, so the ordered choice falls through);
- `**` before `*`.

Two rule sets, `unit` and `brace-unit`, are mutually recursive through `brace`. That is what makes `{a{b,c},d}` work without modes: `,` and `}` terminate at every nesting level.

Deliberate choices:

- **Whitespace skipping is never enabled.** A space is a pattern character.
- **No `EscapedText`.** A backslash is an ordinary character, not an escape.
- **No `Regex` and no `Custom` in the grammar.** A literal is `NegativeLookahead(specials) + AnyChar`; the class body is a choice plus a `ZeroOrMore` of `!']' AnyChar`, which is what lets the first `]` be an element.

### 5.1. Compilation

1. parse to a unit list;
2. `GlobExpander.NormalizeBraces`: a brace with fewer than two branches becomes literal `{`, content, `}`;
3. `GlobExpander.Expand`: brace groups are replaced by their branches (Cartesian product), capped at 64 alternatives;
4. type-specific: segments are split, the prefix is extracted, each remaining segment is compiled by `GlobTextPattern.FromUnits`.

## 6. Units

| Construct | Meaning | Example |
|---|---|---|
| `*` | any run of characters inside a segment, including empty | `*.cs` |
| `?` | exactly one character | `FileUtil?.cs` |
| `**` | zero or more whole segments; must occupy a segment alone | `a/**/b` |
| `[abc]`, `[a-c]` | one character from the set | `[a-c].cs` |
| `[!abc]`, `[^abc]` | one character outside the set | `[!abc].cs` |
| `{a,b}` | alternatives, expanded before matching | `*.{cs,loc}` |
| `{,b}` | an alternative may be empty | `a{,b}/x` |
| `[*]`, `[?]`, `[[]`, `[]]` | literal characters; the only escape mechanism | `[*].cs` matches `*.cs` |
| `\` | an ordinary character, never an escape | `C:\MyFiles\*` |
| `:*` | `GlobOptions.ArgumentTail` only: end of text, or a space/tab and anything | `git:*` |

Malformed constructs are literals: `[a`, `{a`, `{a}` (no comma), `]` and `}` and `,` outside their construct. A reversed class range (`[z-a]`) is an error, never a literal: it is always a typo.

`{abc,def}a*8{bc,ef}[a-g]` is a supported single segment: four alternatives, each compiled to one regular expression.

## 7. Prefix extraction

A segment is **prefix-eligible** when it is a path operator (`.`, `..`, `X:`, a leading separator) or a literal: after decoding escape classes it contains no `*` and no `?`, and its brace expansion yields one alternative.

> **Prefix** is the longest common leading run of prefix-eligible segments across all alternatives.

**The last segment always stays in the remainder.** A fully literal pattern must still name the entry the walk has to find; otherwise the "root" would be the file itself and a walker would have nothing to enumerate.

| Pattern | Prefix | Remainder |
|---|---|---|
| `C:\MyFiles\**\*.txt` | `C:`, `MyFiles` | `**`, `*.txt` |
| `src/**/*.cs` | `src` | `**`, `*.cs` |
| `src/lib/main.cs` | `src`, `lib` | `main.cs` |
| `src/` | `src` | (empty, directory) |
| `C:` | `C:/` | (empty) |
| `{src,lib}/**` | (empty) | unchanged |
| `src/{a,b}/**` | `src` | `{a,b}`, `**` |
| `../shared/**` | `..`, `shared` | `**` |
| `[ab]/x` | (empty) | unchanged |

A leading `./` is **not** stripped: `.` is a path operator and lands in the prefix, where the resolver collapses it. `a//b` collapses empty segments in the split.

`C:foo` is `DriveRelativePattern`. A bare `C:` is rewritten to `C:/` when it ends the prefix, because `Path.GetFullPath("C:")` is drive-relative and silently means "the current directory on C:".

Brace alternatives that disagree on being rooted (`{/a,b}`) are `ConflictingRoot`.

`GlobOptions.NoPrefix` disables extraction: `Prefix` is empty, `Remainder` is the pattern itself, `.`/`..`/`X:` stay ordinary segments, and a `..` segment becomes `ParentSegment`. Matching is unaffected - a literal segment matches only itself either way.

### 7.1. Consequences

- **A typo in the prefix becomes an error, not an empty result.** `serc/**/*.cs` resolves to a root that does not exist; the tool reports it instead of "no matches".
- **The prefix is the access-check unit.** The starting path and the prefix are combined into one absolute root, and that root is what the policy sees. `bin/**/*.cs` starts the walk inside `bin`, so the ignore policy never gets a chance to skip it.
- **A prefix that takes everything makes the root an entry, not a directory.** `GlobWalker` reports the root itself - as a file when it is a file - so `fs-glob README.md` and `fs-grep src/Program.cs` work.

## 8. Resolving a search

```csharp
public static GlobSearch From(string pattern, string startingPath, GlobOptions options = GlobOptions.None);
```

- `startingPath` must be absolute; otherwise `ArgumentException`. Resolving it against a working directory is the caller's job - `Path.GetFullPath` would use the process directory, which is not what any caller means.
- `root = Path.GetFullPath(Path.Combine(startingPath, string.Join('/', pattern.Prefix)))`.
- `Pattern` is the remainder, `StartingPath` is kept for relative output, `DisplayRoot` is the root rendered relative to `StartingPath` (absolute when the prefix pointed elsewhere).
- `WithRoot` exists because the access policy resolves the root after `From` has built it.

`From` never touches the file system and does not check that the root exists: only the caller can turn a missing root into a message that says where it looked.

A rooted prefix simply wins over the starting path. A conflict between an explicitly given `path` and a rooted pattern is reported by the tool, not by `GlobSearch`, because only the tool knows whether `path` was defaulted.

## 9. Matching model

A path is an ordered list of segment names. Matching is a dynamic program over two indices: `p` (path segments consumed) and `s` (pattern segments read).

```
live[0][0] = true, then closure: a '**' at s makes live[s+1] live as well

for p, s ascending:
	if live[p][s]:
		if pattern[s] is '**':   live[p+1][s] = true      // swallows a segment
		                         live[p][s+1] = true      // matches zero
		else if pattern[s] matches path[p]:  live[p+1][s+1] = true
```

`GlobAlternative.IsAcceptingAt(s)` is true when everything from `s` is `**`, which is what makes `a/**` match `a` and `**` match the root.

Per-entry cost is `O(depth x segments)`; the rows are recomputed from the root for every directory rather than threaded down, which is fine for the walks this app does and is the first thing to profile if it ever is not.

### 9.1. Why not one regular expression for the whole pattern

An "adequate" conversion exists:

```
**/  -> (?:[^/]+/)*      **  -> .*      *  -> [^/]*      ?  -> [^/]      {a,b}  -> (?:a|b)
```

and `Matches` could be a single `Regex.IsMatch`. It is not, for three things a whole-pattern regex cannot answer:

1. **`IsPrefixPossible`** is a question about a *prefix* of the input. .NET has no partial-match API, and testing a suffix does not answer it: `a/**/b` must stay live for the prefix `a/x`.
2. **`IsExplicitName`** asks whether the pattern names *this* entry *at this position*. With `**` the depth is not a segment index, so the answer lives in the DP row.
3. **Error positions.** Each segment is compiled separately, so `[z-a]` reports the exact character; a concatenated regex reports nothing usable.

What is not lost: every segment *is* compiled to a regular expression; the DP only chains them. There is no hand-written wildcard matcher anywhere.

A useful future addition is `GlobPattern.ToTextPattern()` - the whole-pattern regex above - which would let `Matches` delegate to it and, more importantly, act as an oracle for a differential test against the DP.

## 10. Ignore policy

There is no `showHidden` flag and no hardcoded skip list. There is one set of **single-segment patterns over entry names**, `GlobTextSet.DefaultIgnore`:

```
.* .git .hg .svn .vs .idea .vscode node_modules __pycache__ .venv bin obj TestResults
```

- Applied to every entry name, files and directories alike.
- A matching directory is not descended into.
- An item may not contain a separator: `**/bin` is `SegmentPatternExpected`, because in a flat pattern a separator is a literal and the rule would silently match nothing.
- `ignore: []` means "skip nothing" and replaces `showHidden: true`.
- The walker never filters the root.

**Explicitly named wins.** If the pattern at its current position names the entry, the list does not skip it. A segment explicitly names a name when it matches it and either contains no `*` and no `?` (braces and classes are finite, so they name) or begins with a literal `.`.

| Pattern | Entry | Walked |
|---|---|---|
| `**/config` | `.git` | no |
| `**/*.cs` | `obj` | no |
| `**/obj/*.cs` | `obj` | yes |
| `{.git,src}/**` | `.git` | yes |
| `.*/config` | `.git` | yes |

There is no `exclude` list: it would be the same mechanism with a different shape.

## 11. Errors

`GlobException : ArgumentException`, with `GlobError Error`, `int Position` in the original pattern, and `string Pattern`.

| `GlobError` | Condition |
|---|---|
| `Empty` | empty or whitespace-only pattern |
| `TooLong` | longer than 1024 |
| `TooManyAlternatives` | more than 64 after expansion |
| `TooManyUnits` | more than 256 units in one alternative |
| `InvalidSyntax` | the grammar cannot describe the input at all |
| `ParentSegment` | `..` used with `NoPrefix` |
| `DriveRelativePattern` | `X:foo` |
| `ConflictingRoot` | brace alternatives disagree on rootedness |
| `DoubleStarNotWholeSegment` | `a**b` |
| `DoubleStarUnsupported` | `**` in a flat pattern |
| `ArgumentTailNotTerminal` | `:*` not at the end |
| `OptionNotSupported` | an option the pattern type cannot honour |
| `InvalidClassRange` | `[z-a]` |
| `NegationUnsupported` | leading `!` |
| `SegmentPatternExpected` | a separator in an ignore item |

No value degrades into "no matches". The tool layer turns a `GlobException` into a failed call carrying the message, and it never reaches the file system.

## 12. Limits and security

`MaxPatternLength = 1024`, `MaxAlternatives = 64`, `MaxUnits = 256` (`GlobLimits`, `GlobExpander`).

Every literal is passed through `Regex.Escape`; from a class only `[`, `]`, `!`, `^`, `-` and characters survive. A pattern is model input and is never interpolated into a regular expression. Generated expressions have no nesting, no groups and no backreferences.

## 13. API

```csharp
namespace LLMDesktopAssistant.Utils.Patterns;

/// <summary>Comparison options for a compiled pattern.</summary>
[Flags]
public enum GlobOptions
{
	None = 0,           // ordinal comparison
	IgnoreCase = 1,
	ArgumentTail = 2,   // enables the terminal ':*'; rejected by GlobPattern
	NoPrefix = 4,       // disables prefix extraction; rejected by GlobTextPattern
}

public sealed class GlobPattern
{
	public static GlobPattern Parse(string pattern, GlobOptions options = GlobOptions.None);

	public IReadOnlyList<string> Prefix { get; }
	public IReadOnlyList<GlobAlternative> Alternatives { get; }
	public IReadOnlyList<string> AlternativeTexts { get; }
	public bool RequiresDirectory { get; }
	public bool IsEmpty { get; }          // no segments left: the root itself is the match
	public GlobPattern Remainder { get; }

	public bool Matches(IReadOnlyList<string> segments);
	public bool IsPrefixPossible(IReadOnlyList<string> segments);
	public bool IsExplicitName(IReadOnlyList<string> prefix, string name);
}

public sealed class GlobAlternative
{
	public GlobAlternative(IReadOnlyList<GlobSegment> segments);
	public IReadOnlyList<GlobSegment> Segments { get; }
	public bool IsAcceptingAt(int position);
}

public readonly record struct GlobSegment(GlobSegmentKind Kind, GlobTextPattern? Pattern);
public enum GlobSegmentKind { Pattern, DoubleStar }

public sealed class GlobTextPattern
{
	public static GlobTextPattern Parse(string pattern, GlobOptions options = GlobOptions.None);
	public static GlobTextPattern FromUnits(IReadOnlyList<GlobUnit> units, string sourcePattern,
		GlobOptions options = GlobOptions.None);

	public IReadOnlyList<string> Alternatives { get; }
	public bool HasUnboundedWildcard { get; }
	public bool StartsWithLiteralDot { get; }

	public bool Matches(string text);
}

public sealed class GlobTextSet
{
	public static GlobTextSet Empty { get; }
	public static GlobTextSet DefaultIgnore { get; }
	public static GlobTextSet ParseNames(IEnumerable<string> patterns, GlobOptions options = GlobOptions.None);
	public bool Matches(string text);
}
```

The AST is public and inspectable: `GlobUnit` with `GlobLiteral`, `GlobAnyChars`, `GlobAnyChar`, `GlobDoubleStar`, `GlobSeparator`, `GlobClass`, `GlobBrace`, all carrying `Position`. `GlobPatternParser`, `GlobExpander` (`Expand`, `NormalizeBraces`, `Render`) and `GlobClassCompiler` are public too - a public AST with an internal way to obtain it is useless.

```csharp
namespace LLMDesktopAssistant.Utils.Files;

public sealed class GlobSearch
{
	public static GlobSearch From(string pattern, string startingPath, GlobOptions options = GlobOptions.None);

	public string SourcePattern { get; }
	public GlobPattern Pattern { get; }
	public string StartingPath { get; }
	public string Root { get; }
	public string DisplayRoot { get; }

	public GlobSearch WithRoot(string root);
}

public sealed record GlobEntry
{
	public required string FullPath { get; init; }
	public required string RelativeToRoot { get; init; }
	public required string Name { get; init; }
	public bool IsDirectory { get; init; }
}

public sealed record GlobWalkOptions
{
	public static GlobWalkOptions FilesOnly { get; }
	public bool Files { get; init; } = true;
	public bool Directories { get; init; }
	public int Limit { get; init; } = 100;
	public GlobTextSet Ignore { get; init; } = GlobTextSet.DefaultIgnore;
}

public static class GlobWalker
{
	public static IEnumerable<GlobEntry> Enumerate(GlobSearch search, GlobWalkOptions options,
		CancellationToken cancellationToken = default);
}
```

`GlobWalker` is static: no state, no dependencies, no policy. It does not check access and does not open files; metrics belong to the caller, after the limit.

## 14. Walk and cost

- Prefix extraction removes the leading walk: `folder1/**/file1.txt` starts at `folder1`.
- The DP is kept for correctness of `**`, not as a pruning device. After the prefix extraction, remaining segments rarely restrict the walk (`**` keeps every state alive), so `IsPrefixPossible` earns little - it is exposed, not relied on.
- Reparse points are not visited; an unreadable directory is skipped; cancellation is checked per entry.
- File metrics are read by the caller, after the limit.

## 15. Test vectors

Normative; every row is a test in `tests/LLMDesktopAssistant.Tests/Utils`.

### 15.1. Syntax

| Pattern | Input | Expect |
|---|---|---|
| `a**b` | - | `DoubleStarNotWholeSegment` |
| `[z-a]` | - | `InvalidClassRange` |
| `""` | - | `Empty` |
| `[a` | `[a` | match (literal) |
| `{a,b` | `{a,b` | match (literals) |
| `a{b}c` | `a{b}c` | match (literals) |
| `{a}` | `{a}` | match (literals) |
| `[]]x` | `]x` | match |
| `[[]x` | `[x` | match |
| `a]b}` | `a]b}` | match |
| `{a,b}` | - | two alternatives |
| `{a{b,c},d}` | - | three alternatives |

### 15.2. Segments

| Pattern | Segments | Expect |
|---|---|---|
| `*.cs` | `a.cs` | match |
| `*.cs` | `src`, `a.cs` | no match |
| `**/*.cs` | `a.cs` | match |
| `**/*.cs` | `src`, `a`, `b.cs` | match |
| `a/**/b` | `a`, `b` | match |
| `a/**/b` | `a`, `x`, `y`, `b` | match |
| `a/**` | `a` | match |
| `**` | (empty) | match |
| `*` | (empty) | no match |
| `?` | `a` | match |
| `FileUtil?.cs` | `FileUtils.cs` | match |
| `[a-c].cs` | `b.cs` | match |
| `[!abc].cs` | `b.cs` | no match |
| `*.lo[c]` | `a.loc` | match |
| `*.{cs,loc}` | `a.loc` | match |
| `{abc,def}a*8{bc,ef}[a-g]` | `abcax8bcd` | match, four alternatives |
| `a//b` | `a`, `b` | match |
| `src/` | `src` | match, `RequiresDirectory` |

### 15.3. Prefix

| Pattern | Prefix | Remainder |
|---|---|---|
| `C:\MyFiles\**\*.txt` | `C:`, `MyFiles` | `**`, `*.txt` |
| `src/**/*.cs` | `src` | `**`, `*.cs` |
| `src/lib/main.cs` | `src`, `lib` | `main.cs` |
| `{src,lib}/**` | (empty) | unchanged |
| `../shared/**` | `..`, `shared` | `**` |
| `C:` | `C:/` | (empty) |
| `./a/b` | `.`, `a` | `b` |

### 15.4. Flat and command

| Pattern | Text | Expect |
|---|---|---|
| `git:*` with `ArgumentTail` | `git` | match |
| `git:*` with `ArgumentTail` | `git status`, `git\tstatus`, `git ` | match |
| `git:*` with `ArgumentTail` | `gitty`, `git\nstatus` | no match |
| `git:*` without the flag | `git` | no match |
| `*.cs` | `a/b.cs` | match |
| `*` | (empty) | match |
| `[*]` | `*` | match |
| `tool.*.fs-glob` | `tool.name.fs-glob` | match |

### 15.5. Walk

| Scenario | Expect |
|---|---|
| `**/*.cs` with `obj/` present | `obj` not walked |
| `**/obj/*.cs` | `obj` walked |
| `**/config` with `.git/config` present | absent |
| `.git/**/*` | `.git` walked |
| `src/` with `Directories = true` | one entry, the root, `RelativeToRoot` empty |
| `readme.md` relative to `docs/` | one entry, the file |
| `src` with `Files = true` | no entries |
| limit 1 | one entry |
| missing root | no entries |

## 16. Migration

| Where | Before | After |
|---|---|---|
| `FilesystemGlobToolModule` | `FileSystemGlobbing.Matcher` | `GlobPattern` + `GlobSearch` + `GlobWalker`; `showHidden` replaced by `ignore` |
| `FilesystemExploreToolModule` | `FileSystemName.MatchesSimpleExpression` | `GlobTextSet.ParseNames` |
| `FilesystemGrepToolModule` | `Directory.EnumerateFiles` + `allowedExtensions` + `recursive` | one glob path; `GlobWalker` collects the files |
| `WebReaperWebBrowserInstaller` | `Directory.EnumerateDirectories(root, "chromium-*")` | not migrated |
| `SpecifierMatcher.GlobMatch` | hand-rolled `*`, `?`, `:*` recursion | not migrated |

`fs-glob` description shrank to what the model cannot infer:

```
Finds files and directories by glob pattern.
Syntax: *, ?, [...], {...}, **. No backslash escaping; '**' must be a whole segment.
A leading directory in the pattern becomes the search root, so an absolute pattern works.
Syntax the dialect does not cover fails with a reason instead of returning no matches.
```

### 16.1. Specifier audit

Before `[]` and `{}` are switched on for specifiers, every existing rule in the repository and in the installed packs must be listed and checked: a rule containing `[` or `{` changes meaning, because those characters become syntax. `SpecifierMatcherTests` is the safety net.

## 17. Why not bash compatibility

- Completeness of the *description* matters more than completeness of the *syntax*: a pattern the engine cannot honour must fail, not lie about the file system.
- The ripgrep dialect is not available: the core project is portable and an external process is out of the question.
- `extglob` and POSIX classes are needed by none of the consumers.

## 18. Specifiers

- Specifier literals that target the main argument are prose and compile as `GlobTextPattern` with `ArgumentTail`: `git:*`, `rm -rf:*`.
- Path-typed specifier targets arrive together with analyzers generated from `[MainArgument]` and `[PathArgument]`; until then no rule can express one, which keeps `SpecifierMatcher.Match` unchanged.
- `SpecifierMatcher` keeps the coverage logic (`FullMatch` requires every main argument part to be covered). Only its private `GlobMatch` disappears.
- A tool that needs the paths inside a command line gets them from its own `IToolSpecifierAnalyzer`, which tokenizes. The matcher never parses a command line.

## 19. Open

| # | Question |
|---|---|
| 1 | Whether `ignoreDirectories` in `fs-read_entry` is renamed to `ignore`. The mechanism is now the same as `fs-glob`; only the name differs. |
| 2 | `GlobPattern.ToTextPattern()` as a whole-pattern regex: an implementation shortcut for `Matches` and an oracle for differential tests. |
| 3 | Threading DP rows down the walk instead of recomputing them per directory, if a walk ever measures slow. |
| 4 | `WebReaperWebBrowserInstaller` still uses the Windows-only enumeration overload. |
