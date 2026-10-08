# 15: Command fingerprint and execution status

Status: ready-for-agent
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

- [ ] `SlashCommandFingerprint` and `SlashCommandExecutionStatus` exist with the shape above; the fingerprint is
      data-only and persisted.
- [ ] `SlashCommandExecutionResult` carries `EffectSummary` (optional, defaulting to `null`).
- [ ] `SlashCommandInsertionService` writes the fingerprint on success, failure and cancellation, with the correct
      `Status`, `Error` and `GenerateOutcome`, and attaches it to the message.
- [ ] A persisted fingerprint round-trips through the `AdditionalChatData` synchronizer (read back via
      `AdditionalData.TryGet<SlashCommandFingerprint>()`).
- [ ] The fingerprint is never rendered into the prompt.
- [ ] Tests: building the fingerprint from a bound-arguments + result pair (pure), and a round-trip on
      `ChatStorageTestContext`.
- [ ] The solution builds; the full test suite stays green.

## Answer

<!-- appended on resolution -->

## Comments

- Stage 2 grilling: the fingerprint is written after execution (not "token before / outcome after"), `RawText` is
  dropped (it is the message `Content`), and `EffectSummary` gets its channel on the result.
