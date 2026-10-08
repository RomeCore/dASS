# 16: Command source kind

Status: resolved
Type: task
Blocked by:

## What to build

Give a command an explicit, typed origin instead of inferring it. A new `SlashCommandSource` enum names what a command
was derived from, a derived provider stamps it onto every command it produces, the source-priority tier is derived
from it, and the command fingerprint records it (replacing the source addon's type-name string).

### Types

```csharp
public enum SlashCommandSource { Unknown = 0, Native = 1, Script = 2, Skill = 3, SubAgent = 4, Tool = 5 }
```

- `SlashCommandInfo.SourceKind : SlashCommandSource` — serialized, defaults to `Unknown`, set alongside the existing
  runtime-only `Source` (the source addon object).
- `DerivedSlashCommandProvider<TSource>.SourceKind` — a new abstract member; `SkillSlashCommandProvider` returns
  `Skill`, `SubAgentSlashCommandProvider` returns `SubAgent`.
- `SlashCommandOrderTiers.ForSource(SlashCommandSource)` → `Native` / `Scriptable` / `Derived`; the providers compute
  `OverrideOrder` from it instead of hard-coding `Derived`. The tiers stay the ordering authority — `SourceKind` is
  descriptive.
- `SlashCommandFingerprint.Source` (`string?`, the source addon type name) becomes `SourceKind : SlashCommandSource`
  (`Unknown` for a command with no source, or an unresolved token).

## Acceptance criteria

- [x] `SlashCommandSource` exists with `{ Unknown, Native, Script, Skill, SubAgent, Tool }`.
- [x] `SlashCommandInfo.SourceKind` exists (default `Unknown`) and is serialized.
- [x] `DerivedSlashCommandProvider<TSource>` declares an abstract `SourceKind`; the skill and sub-agent providers return
      `Skill` / `SubAgent`.
- [x] `OverrideOrder` is derived from `SourceKind` through `SlashCommandOrderTiers.ForSource`; the tier constants are
      unchanged.
- [x] `SlashCommandFingerprint` carries `SourceKind` instead of the source type-name string, and it round-trips.
- [x] Tests updated (`SourceKind` on the derived command and on the fingerprint), plus a mapping test for
      `SlashCommandOrderTiers.ForSource`.
- [x] The solution builds; the (filtered) test suite stays green.

## Answer

- **`SlashCommandSource`** (`SlashCommands/SlashCommandSource.cs`): `{ Unknown = 0, Native = 1, Script = 2, Skill = 3,
  SubAgent = 4, Tool = 5 }`. Descriptive — it drives the card source chip and the fingerprint.
- **`SlashCommandInfo.SourceKind`** (`SlashCommandSource`, default `Unknown`, serialized) sits next to the runtime-only
  `object? Source`. `DerivedSlashCommandProvider<TSource>` gained an abstract `SourceKind`, stamped as
  `SourceKind = this.SourceKind` on every command; the skill/sub-agent providers override it with `Skill` / `SubAgent`.
- **`SlashCommandOrderTiers.ForSource`** maps `Native → Native (2000)`, `Script → Scriptable (1000)`, everything else
  (including `Unknown`) `→ Derived (0)`; the provider now sets `OverrideOrder = SlashCommandOrderTiers.ForSource(SourceKind)`.
  The tier constants stay the ordering authority.
- **Fingerprint.** `SlashCommandFingerprint.Source` (the source addon's `GetType().Name`) is replaced by
  `SourceKind : SlashCommandSource`, filled from `command?.SourceKind ?? SlashCommandSource.Unknown`. This supersedes
  the `Source`-as-type-name shape recorded in ticket 15; there are 0 users and no migrations, so the persisted shape
  changes freely. The BSON round-trip test asserts the enum survives.
- **Tests.** `SlashCommandProviderTests` assert `SourceKind` on the derived command (skill → `Skill`, agent →
  `SubAgent`); `SlashCommandFingerprintTests` assert it on the built and the round-tripped fingerprint (and `Unknown`
  for an unresolved token); `SlashCommandInfoTests` gain `OrderTiers_MapTheSourceKindOntoTheTier`.

Builds: main + desktop green. Slash-command test set: 127 passed.

## Comments

- The chat commands settings tab (ticket 17) consumes `SourceKind` for the card's source chip.
