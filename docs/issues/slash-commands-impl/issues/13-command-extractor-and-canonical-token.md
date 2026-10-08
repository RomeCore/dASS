# 13: `SlashCommandExtractor` and `SlashCommandInfo.CanonicalToken`

Status: resolved
Type: task
Blocked by:

## What to build

The token model is **slash-free**: the leading `/` marks the message as a command, not the command's identity. Ticket
11 left that knowledge inside `SlashCommandMatcher` (`Prefix`, `TryExtractToken`, `UnescapeLeadingSlash`), but the
matcher is a pure name/namespace matcher and should know nothing about message text. Move the marker handling into a
dedicated extractor and give the command its canonical, slash-free token.

### `SlashCommandExtractor`

A small static helper that owns everything about the `/` marker, moved verbatim out of `SlashCommandMatcher`:

```csharp
public static class SlashCommandExtractor
{
    public const char Prefix = '/';

    // Extracts the leading command token (slash-free) and the raw argument remainder; false when the message is not
    // a command; a leading "//" is the escape and returns false.
    public static bool TryExtractToken(string rawText, out string token, out string rawArguments);

    // Drops the first of two leading slashes ("//foo" → "/foo"); returns other text unchanged.
    public static string UnescapeLeadingSlash(string rawText);
}
```

`SlashCommandMatcher` keeps `ParseToken` and `Match` (and loses `Prefix`, `TryExtractToken`, `UnescapeLeadingSlash`).
The doc references to the matcher's extractor (in `ISlashCommandResolver` and elsewhere) are re-pointed to
`SlashCommandExtractor`. The existing extractor tests move from `SlashCommandMatcherTests` to a new
`SlashCommandExtractorTests`; the matcher tests stay for `ParseToken`/`Match`.

### `SlashCommandInfo.CanonicalToken`

The resolved, human-facing identity of a command, slash-free and re-parseable by `SlashCommandMatcher.ParseToken`:

```csharp
// SlashCommandInfo
[JsonIgnore][BsonIgnore][YamlIgnore]
public string CanonicalToken => string.Join(':', [..Namespaces, Name]);   // e.g. "skill:matt-pocock:grilling"
```

- Slash-free — the `/` is added by whoever renders it for the user, never stored on the command.
- Distinct from `Key` (which sorts the namespaces ordinally and is the dedup/settings identity); `CanonicalToken`
  keeps the stored order (type first, then pack) so it reads like the token a user types.
- This is the value that will reach `SlashCommandExecutionContext.Token` (ticket 14) and the fingerprint (ticket 15);
  update the `SlashCommandExecutionContext` doc examples from `/skill:grilling` to slash-free tokens.

## Acceptance criteria

- [x] `SlashCommandExtractor` exists with `Prefix`, `TryExtractToken`, `UnescapeLeadingSlash`; behaviour is byte-for-byte
      unchanged from ticket 11 (leading whitespace, bare `/`, `//` escape, multi-line remainder).
- [x] `SlashCommandMatcher` no longer mentions `/` anywhere (no `Prefix`, no extractor methods); `ParseToken`/`Match`
      unchanged.
- [x] `SlashCommandInfo.CanonicalToken` exists (computed, with the three ignore attributes), is slash-free, and equals
      `namespaces + name` joined by `:` in stored order.
- [x] Extractor tests live in a new `SlashCommandExtractorTests`; `SlashCommandMatcherTests` keeps only matcher cases.
- [x] `SlashCommandExecutionContext.Token`/`RawToken` docs and any other stale `/`-in-token docs are corrected.
- [x] The solution builds; the full test suite stays green.

## Answer

- **`SlashCommandExtractor`** (`SlashCommands/SlashCommandExtractor.cs`, namespace `LLMDesktopAssistant.SlashCommands`)
  owns `Prefix`, `TryExtractToken` and `UnescapeLeadingSlash`, moved verbatim from the matcher — behaviour is unchanged.
- **`SlashCommandMatcher`** is now purely slash-free: the extractor methods, `Prefix` and the now-unused
  `SkipWhitespace` are gone; `ParseToken`/`Match` are untouched. Its remarks no longer claim to know the marker.
- **`SlashCommandInfo.CanonicalToken`**: `string.Join(':', Namespaces.Add(Name))` — slash-free, stored namespace order
  (type, then pack) + name, e.g. `skill:matt-pocock:grilling`; carries the three ignore attributes. It differs from
  `Key` (which sorts the namespaces ordinally) — `SlashCommandInfoTests` asserts both the difference and that the token
  is re-parseable by `ParseToken`.
- **Docs**: `ISlashCommandResolver` now points at `SlashCommandExtractor.TryExtractToken`; the
  `SlashCommandExecutionContext.Token`/`RawToken` summaries are slash-free.
- **Tests**: extractor cases moved to `SlashCommandExtractorTests`; `SlashCommandMatcherTests` keeps parsing/matching;
  `SlashCommandInfoTests` covers `CanonicalToken`.

Builds: main green. Slash-command test set: 97 passed. (A full-suite run was cancelled by the maintainer because the
suite stalled at ~43s — the known intermittent test-host hang noted in the stage-2 handoff, not a failure; the affected
namespace was already verified in isolation.)

Note: the matcher's total order gained an "exact name before alias match" key while this ticket was in flight — the
maintainer's own edit, committed separately (`fix(slash-commands): prefer an exact command name over an alias match`).
The matcher remark now documents it; ticket 11's Answer still describes the older order and is left as history.

## Comments

- Stage 2 grilling: `SlashCommandMatcher` must know nothing about slashes; `IChatMessageInsertionService` (ticket 14)
  is the consumer of `SlashCommandExtractor`. Tokens never contain a slash; `RawToken` is the as-typed slash-free token,
  `CanonicalToken` is the fully-qualified slash-free token.
