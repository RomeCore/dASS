# 11: Command resolver and namespacing

Status: resolved
Type: task
Blocked by: 10

## What to build

Turning a typed token into exactly one command: the pure matcher, the chat-scoped resolver and the resolution result it
returns to the execution path and to argument autocomplete.

Files live under `src/LLMDesktopAssistant/SlashCommands/Resolution/`.

### Token grammar

A message is a command message when its **leading** whitespace-delimited word starts with `/`. That word is the
**token**; the rest of the message is the raw argument text. A message whose leading word starts with `//` is
**escaped** — it is not a command, and one leading slash is stripped (`//foo` → `/foo`).

The token model is **slash-free**: the leading `/` is a marker of the message text, not of the command's identity.
The only place that knows the marker is `SlashCommandMatcher.TryExtractToken` (reused by the message-insertion and
autocomplete services); everything downstream — `SlashCommandToken`, the resolver, the stored `Token` — is slash-free.

The token is `:`-separated: the **last** segment is the command name, every preceding segment is a **namespace
qualifier** that must match one of the command's `Namespaces` (set semantics, case-insensitive — a namespace set is
compared as a set, not as a sequence). Examples:

```
/grilling                      name only
/skill:grilling                by type
/matt-pocock:grilling          by pack
/skill:matt-pocock:grilling    by both
```

`Aliases` resolve on par with `Name`; matching is case-insensitive throughout.

### Matcher (pure, unit-tested)

```csharp
public readonly record struct SlashCommandToken(
    string Raw, ImmutableList<string> Qualifiers, string Name, bool IsValid);

public readonly record struct SlashCommandCandidate(SlashCommandInfo Command, bool IsDefeated);

public static class SlashCommandMatcher
{
    // Extracts the leading command token and the raw argument remainder; false when the message is not a command.
    public static bool TryExtractToken(string rawText, out string token, out string rawArguments);

    // Splits "skill:matt-pocock:grilling" (slash-free) into qualifiers + name; IsValid = non-empty name, no empty
    // segments.
    public static SlashCommandToken ParseToken(string token);

    // Undoes the "//" escape: drops the first of two leading slashes; returns the text unchanged otherwise.
    public static string UnescapeLeadingSlash(string rawText);

    // Every command whose name (or alias) matches and whose namespaces satisfy every qualifier,
    // ordered by the total order below. The first entry is the winner, the rest are defeated.
    public static ImmutableList<SlashCommandCandidate> Match(
        IEnumerable<SlashCommandInfo> commands, SlashCommandToken token);
}
```

**Total order** (deterministic — a true tie is impossible by construction):

1. `OverrideOrder` descending — this is where `native > scriptable > derived` is encoded (ticket 09);
2. `Order` ascending;
3. the fully-qualified key (`namespaces` sorted ordinally, joined by `:`, then the name) ascending — the same key the
   collector deduplicates by.

Level 3 only ever decides between commands that were *not* collapsed by the collector (different type/pack), so the
outcome is stable and reproducible, never input-order dependent. There is therefore no `Ambiguous` state: the winner of a
bare name is exactly the "won over others" command the UI paints orange.

### Resolution result

```csharp
public enum SlashCommandResolutionStatus { Unknown, Exact, WonOthers }

public record SlashCommandResolution(
    SlashCommandInfo? Command,
    LocaleKeyBase? Error,
    SlashCommandResolutionStatus Status,
    IReadOnlyList<SlashCommandInfo> Defeated);
```

- `Unknown` — nothing matched; `Command` is `null`, `Error` is `command.error.unknown` (the only case that carries an
  error and blocks the send).
- `Exact` — one match, `Defeated` is empty.
- `WonOthers` — the winner had defeated competitors; `Defeated` carries them (other matches **plus** the winner's
  `Overrides` from the collector's deduplication), so the UI can show their qualified forms.

### Resolver

```csharp
public interface ISlashCommandResolver
{
    SlashCommandResolution Resolve(string token);
}
```

`SlashCommandResolver` is `[ChatService(typeof(ISlashCommandResolver))]` over `IAddonSetCollector<SlashCommandInfo>`,
using `GetAddonsForChat()` (the composite: providers + future file commands). It is reused by the execution path
(ticket 05) and by argument autocomplete in Stage 5 (which resolves the token to a `SlashCommandInfo` in order to reach
its `ArgumentSchema`); it does **not** implement token autocomplete — that belongs to the general autocomplete service.

## Acceptance criteria

- [x] `SlashCommandToken`, `SlashCommandCandidate`, `SlashCommandMatcher`, `SlashCommandResolutionStatus`,
      `SlashCommandResolution`, `ISlashCommandResolver` and `SlashCommandResolver` exist.
- [x] `TryExtractToken` handles: no leading `/`, a bare `/`, `/name`, `/name args`, leading whitespace, and a
      multi-line remainder (arguments keep everything after the token).
- [x] `ParseToken` splits qualifiers/name, rejects empty segments and case-normalises for matching.
- [x] `Match` covers: bare name; by type (`/skill:`); by pack (`/pack:`); by both; alias; set-semantics namespace
      matching (`/pack:skill:name` resolves too); no match → empty list; commands with the same name in different
      namespaces both match a bare token; deterministic ordering by the three-level total order.
- [x] `Resolve` returns `Unknown` + `command.error.unknown` for a token with no match, `Exact` with empty `Defeated` for
      a unique match, and `WonOthers` with the defeated set for a shadowed name.
- [x] `WonOthers` includes the winner's `Overrides` (collapsed duplicates).
- [x] Locale key `command.error.unknown` exists in `iv` and `ru-RU`.
- [x] The solution builds; the full test suite stays green.

## Answer

Implemented under `src/LLMDesktopAssistant/SlashCommands/Resolution/`: `SlashCommandToken`,
`SlashCommandCandidate`, `SlashCommandMatcher`, `SlashCommandResolutionStatus`, `SlashCommandResolution`,
`ISlashCommandResolver` and `SlashCommandResolver`.

### Slash-free tokens (reworked from the ticket)

The token model carries **no leading `/`** — the marker belongs to the message text, not to the command's identity.
`TryExtractToken` is the one place that knows it: it skips leading whitespace, requires the first non-whitespace
character to be `/`, and strips it from the returned token. `SlashCommandToken.Raw` is slash-free (`skill:grilling`),
and `IsValid` is now "non-empty name and no empty segments" (the `startsWith('/')` check moved to the message level).
Docs and examples keep `/` where it means "what the user types".

### The `//` escape

A message whose first non-whitespace word starts with `//` is **not** a command: `TryExtractToken` returns `false`, and
`SlashCommandMatcher.UnescapeLeadingSlash` drops the first of the two slashes so the host can send the intended text
(`//foo` → `/foo`). Whitespace before the marker is preserved; a normal message or a lone leading `\` is untouched.

### Matcher

`TryExtractToken` — any whitespace character separates; the argument remainder is the text after the token with the
separating whitespace trimmed off the left, kept verbatim otherwise (newlines included). `ParseToken` — `:` split, last
segment the name, `IsValid` as above. `Match` — name-or-alias and qualifier-set-subset, both case-insensitive, ordered
by `OverrideOrder` desc → `Order` asc → `Key` asc, so a true tie cannot occur. `Match` is pure and knows nothing about
the collector's `Overrides`.

### Resolver

`SlashCommandResolver` (`[ChatService(typeof(ISlashCommandResolver))]`) over `IAddonSetCollector<SlashCommandInfo>`
using `GetAddonsForChat()`. `Unknown` (nothing matched or the token is invalid) carries `command.error.unknown`;
`Exact` has an empty `Defeated`; `WonOthers` carries `matches.Skip(1) ++ winner.Overrides` — the de-duplication
artifacts are folded in by the resolver, not the matcher.

### Tests

`SlashCommandMatcherTests` + `SlashCommandResolverTests` (37 cases): the extractor cases incl. the escape, the
multi-line remainder, `ParseToken`, matching by bare/type/pack/both/alias, case-insensitivity, no-match, same name in
different namespaces, the total order (tier → order → key), and the three resolver statuses incl. `Overrides` in
`WonOthers`.

Locale key `command.error.unknown` added to `iv` and `ru-RU`. Main + desktop builds green; full suite: 895 total,
894 passed, 1 skipped (pre-existing).

## Comments

- **Escape hatch agreed during grilling**: `//` at the start of the (whitespace-trimmed) message means "not a command"; one leading slash is stripped. Token model made slash-free, with `/` knowledge kept in `TryExtractToken` /
  `UnescapeLeadingSlash` (the message-insertion service and the input autocomplete service reuse them).
- `Ambiguous` from the design ticket is dropped during implementation: the collector collapses same-key duplicates and
  the matcher's three-level total order makes the remaining outcome deterministic, so a true tie cannot occur. The UI
  state formerly called "ambiguous" (orange + underline) is `WonOthers`; `Unknown` (red + underline) is unchanged.
