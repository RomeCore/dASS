# 15: Command fingerprint and execution status

Status: resolved
Type: task
Blocked by: 12, 14

## What to build

The machine-readable trace of a command invocation, attached to the message the command produced: what was typed, what
resolved, how it was interpreted, how it ran. It is data-only (never rendered into the prompt) but persisted, so it
survives a reload and can be read back — the trace the Lua API and any future command UI consume.

### Types

```csharp
public enum SlashCommandExecutionStatus { Executed, Failed, Cancelled }

public class SlashCommandFingerprint : AdditionalChatData
{
    public required string Token { get; init; }                        // canonical, slash-free, e.g. "skill:matt-pocock:grilling"
    public required string CommandName { get; init; }
    public ImmutableList<string> Namespaces { get; init; } = [];
    public string? Source { get; init; }                               // provider / addon source label
    public ImmutableList<string> PositionalArguments { get; init; } = [];        // raw strings, as written
    public ImmutableDictionary<string, string> KeyedArguments { get; init; } = [];// raw strings, as written
    public string RestPositionalArguments { get; init; } = "";                   // verbatim surplus text
    public ModelFacingMode ModelFacingMode { get; init; }
    public bool GenerateIntent { get; init; }
    public bool GenerateOutcome { get; init; }
    public SlashCommandExecutionStatus Status { get; init; }
    public LocaleKeyBase? Error { get; init; }
    public string? EffectSummary { get; init; }
}
```

- `Token` is slash-free (ticket 13); there is **no** `RawText` field — the raw text is the message `Content`, read from
  the message itself.
- `Error` is a `LocaleKeyBase?` (ticket 12); `Error`'s raw strings only, never live addon or `Value` objects, so the
  trace stays stable when addons change. `PositionalArguments` / `KeyedArguments` carry the user-written raw text, and
  `RestPositionalArguments` preserves the verbatim surplus (the only place `/skill`'s argument text lives structurally).
- Data-only: `IsVisible = false`, `IsTemporary = false` (persisted), so it round-trips through the existing
  `AdditionalChatData` synchronizer.

### Who writes it

The host (`IChatMessageInsertionService`, ticket 14) — **after** the command ran (or after the guard rejected it), so
the whole trace is known in one shot and the properties can stay `init`-only. The outcome fields (`GenerateOutcome`,
`Status`, `Error`, `EffectSummary`) are filled from the executor result or the failure. On success `Status = Executed`;
on a bind/resolve failure `Status = Failed` with the failure's `Error`; on cancellation `Status = Cancelled`. The
fingerprint is attached to the produced message's `AdditionalData`.

### Effect summary channel

`SlashCommandExecutionResult` gains an optional `EffectSummary` so an executor can describe its effect for the trace:

```csharp
public readonly record struct SlashCommandExecutionResult(bool Generate, LocaleKeyBase? Error, string? EffectSummary = null);
```

The stub executor leaves it `null`; the real `/skill` and `/agent` executors (Stage 3) fill it.

## Acceptance criteria

- [x] `SlashCommandFingerprint` and `SlashCommandExecutionStatus` exist; the fingerprint is data-only and persisted.
- [x] `SlashCommandExecutionResult` carries `EffectSummary` (optional, defaulting to `null`).
- [x] `ChatMessageInsertionService` writes the fingerprint on success, failure and cancellation, with the correct
      `Status`, `Error` and `GenerateOutcome`, and attaches it to the message.
- [x] A persisted fingerprint round-trips through the `AdditionalChatData` synchronizer (read back via
      `AdditionalData.TryGet<SlashCommandFingerprint>()`).
- [x] The fingerprint is never rendered into the prompt.
- [x] Tests: building the fingerprint from a bound-arguments + result pair (pure), and a round-trip through the
      additional-data synchronizer.
- [x] The solution builds; the (filtered) test suite stays green.

## Answer

- **`SlashCommandFingerprint : AdditionalChatData`** + `SlashCommandExecutionStatus { Executed, Failed, Cancelled }`.
  `IsVisible = false` (data-only), `IsTemporary` stays `false` (persisted), so it round-trips and is read back with
  `AdditionalData.TryGet<SlashCommandFingerprint>()`. `Create(token, command?, arguments?, …)` builds the snapshot;
  `command`/`arguments` are `null` for an unresolved token, so the trace is then built from the token alone (name +
  qualifiers via `SlashCommandMatcher.ParseToken`).
- **`EffectSummary`** is a new optional field on `SlashCommandExecutionResult`, recorded in the trace.
- **Write timing.** The host writes the fingerprint **after** the command ran (or after the guard rejected it) — one
  shot, so the properties can stay `init`-only; it is attached to the produced message's `AdditionalData`. Success →
  `Executed`; an executor-returned error or a thrown exception → `Failed` (message `Error` set); a cancellation →
  `Cancelled` (no message error). The guard path (unresolved token / bad arguments) also writes a `Failed` trace.
- **Deviations from the ticket's shape**, all forced by LiteDB / recorded during the grilling:
  - `RawText` is dropped (the raw text is the message `Content`);
  - `Token` is slash-free (`command.CanonicalToken`);
  - the collections are `IReadOnlyList<string>` (the `Immutable*` collections do not round-trip through LiteDB) and the
    keyed map is a concrete `Dictionary<string, string>` (`IReadOnlyDictionary` and `ImmutableDictionary` do not
    round-trip either — the round-trip test caught this);
  - `Source` is the source addon's type name (e.g. `SkillInfo`) or `null`.

- **Tests.** `SlashCommandFingerprintTests` (building from a resolved invocation, building from an unresolved token, and
  a BSON round-trip through `AdditionalChatDataSynchronizer`), plus four cases in `ChatMessageInsertionServiceTests`
  asserting the recorded `Status`/`Error`/`GenerateOutcome` for executed / unresolved / executor-error / cancelled.

Builds: main green. Slash-command test set: 126 passed.

## Comments

- Stage 2 grilling: the fingerprint is written after execution (not "token before / outcome after"), `RawText` is
  dropped (it is the message `Content`), and `EffectSummary` gets its channel on the result.
