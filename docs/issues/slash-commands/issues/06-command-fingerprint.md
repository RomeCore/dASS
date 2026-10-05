# Command fingerprint

Status: resolved
Type: grilling
Blocked by: 01

## Question

Design the **machine-readable trace** of a command invocation (the "command fingerprint"): an `AdditionalChatData` part attached to the produced message.

Cover:

- the fields: command name / namespace, source, raw text, parsed arguments, `ModelFacingMode`, resolved `Generate` outcome, status / result;
- persistence (the storage synchronizers for `AdditionalChatData`);
- rendering in the chat UI (how a command message shows the invocation);
- whether and how the fingerprint is exposed to agents (v1: agents see raw content; the fingerprint is a trace).

## Answer

### Type & lifecycle

- `SlashCommandFingerprint : AdditionalChatData` — **data-only** (`IsVisible = false`) and **persisted** (`IsTemporary = false`), so it survives a reload; read back via `AdditionalData.TryGet<SlashCommandFingerprint>()`.
- It is **not** a chip. The visible badge ("used skill 'grilling'") comes from the command's `AdditionalMessageContentPart` (ticket 04).
- The **host** (`IChatMessageInsertionService`, ticket 05) creates the fingerprint and owns its lifecycle: it writes the token / command / intent **before** execution and the outcome / status / error / effect summary **after**.

### Shape — a flat, BSON-safe snapshot

```csharp
public class SlashCommandFingerprint : AdditionalChatData
{
    public required string Token { get; init; }                       // e.g. "/skill:grilling"
    public required string CommandName { get; init; }
    public ImmutableList<string> Namespaces { get; init; } = [];      // type + pack
    public string? Source { get; init; }                             // provider / addon source
    public required string RawText { get; init; }                    // the raw argument text
    public ImmutableList<string> PositionalArguments { get; init; } = [];       // raw strings
    public ImmutableDictionary<string, string> KeyedArguments { get; init; } = []; // raw strings
    public ModelFacingMode ModelFacingMode { get; init; }
    public bool GenerateIntent { get; init; }
    public bool GenerateOutcome { get; init; }
    public SlashCommandExecutionStatus Status { get; init; }
    public string? Error { get; init; }
    public string? EffectSummary { get; init; }                      // e.g. "sub-agent task launched (id)"
}

public enum SlashCommandExecutionStatus { Executed, Failed, Cancelled }
```

- **No references to frozen addons or live `Value` objects** — raw strings only, so the trace stays stable when addons change.
- `LocaleKeyBase` *is* BSON-serializable (noted by the user), so it may be used where it reads better.

### Agent exposure

- The fingerprint is **not** rendered into the prompt — it is a trace.
- `ModelFacingMode` governs how the **message content** renders for agents (v1: `Raw`), **not** the fingerprint.

### Status / result

- `Status ∈ { Executed, Failed, Cancelled }`, a nullable `Error`, and an optional `EffectSummary`. Structured results are **fog**.

## Comments

- Single round. `LocaleKeyBase` acknowledged as BSON-serializable.
