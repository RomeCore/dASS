# Command resolution and namespacing

Status: resolved
Type: grilling
Blocked by: 01

## Question

How does a typed token resolve to **exactly one** command?

Cover:

- the **namespace model**: a type namespace (`skill` / `agent`) plus an optional pack-contributed namespace;
- the "optional while there is no conflict" rule, and what "no conflict" means precisely;
- the source-priority tie-break: **native > scriptable > derived**;
- how a command's *set* of namespaces is built and presented (autocomplete display, de-duplication);
- the exact matching / precedence algorithm;
- the collision case: `/skill:grilling` vs bare `/grilling`, and what happens on an ambiguous name.

Depends on the registry item model from ticket 01.

## Answer

### Namespaces

- A command carries an ordered set of namespaces: its **type** and, when it comes from a pack, its **pack** (`SourcePack.Name`).
- Type namespaces: v1 uses `skill` and `agent`; `tool` (tool commands) and `script` (scriptable commands) are **reserved** for the future.
- Native commands have **no** pack namespace.

### Token grammar

- A command token is `:`-separated; the **last** segment is the command name, all preceding segments are **namespace qualifiers** — each must match one of the command's namespaces:
  - `/grilling` — name only;
  - `/skill:grilling` — by type;
  - `/matt-pocock:grilling` — by pack;
  - `/skill:matt-pocock:grilling` — by both.

### Matching

- **Case-insensitive**; names are normalized/validated exactly as by the source kind (`SkillName`, `SubAgentName`, …).
- `Aliases` resolve on par with `Name`.

### Conflicts

- A bare `/name` resolves by **priority**: candidates are ordered `OrderBy(OverrideOrder).ThenBy(Order)` (higher `OverrideOrder` wins — that is where the **native > scriptable > derived** tier is encoded, since providers assign `OverrideOrder`); the losers stay reachable **only through a namespace**.
- The **UI must mark the defeated commands** (the autocomplete shows the qualified variants of a shadowed name).
- `Ambiguous` is reserved for a true tie (equal `OverrideOrder` **and** `Order`), not for an ordinary shadowed name.

### Dedup key

- `AddonSetCollectorBase` gains a `virtual` deduplication key: `GroupBy(s => s.Name)` becomes `GroupBy(GetDeduplicationKey)`, with the default `GetDeduplicationKey => Name`.
- `SlashCommandSetCollector` overrides it with the **fully-qualified** key (`type [ + pack] + name`), so commands of different types/packs never collapse into one another. Conflicts are then decided by the **resolver**, not by the collector.

### Resolver

- Chat-scoped `ISlashCommandResolver` over `IAddonSetCollector<SlashCommandInfo>`:

```csharp
public record SlashCommandResolution(SlashCommandInfo? Command, LocaleKeyBase? Error);
// Error distinguishes Unknown (nothing matched) from Ambiguous (a true tie)
```

- Error is a `LocaleKeyBase`. The resolver is reused by the dispatch service (ticket 05) and by the autocomplete (tickets 08/09).

## Comments

- Claimed and resolved in one exchange.
- `OrderBy(OverrideOrder).ThenBy(Order)` is the exact precedence the collector already uses; `OverrideOrder` dominates.
