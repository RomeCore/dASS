# 02: Add `GetDeduplicationKey` to `AddonSetCollectorBase`

Status: resolved
Type: task
Blocked by:

## What to build

Make the addon-set deduplication key overridable so that a collector whose items are not unique by name alone
(the upcoming `SlashCommandSetCollector`, where names may collide across namespaces) can group by a
fully-qualified key, while every existing collector keeps grouping by `Name`.

The key is an arbitrary `object` (compared by equality) rather than a `string`, so a collector can return a
composite key (a tuple/record) without delimiter-collision concerns.

## Acceptance criteria

- [x] `AddonSetCollectorBase<TAddon, TChange>` exposes a `protected virtual object GetDeduplicationKey(TAddon)` (default: the addon name).
- [x] `GetAvailableAddons` groups by that key instead of `GroupBy(s => s.Name)`.
- [x] Existing collectors (`ToolSetCollector`, `SkillSetCollector`, `SubAgentSetCollector`, …) behave exactly as before.
- [x] A unit test covers: the default key groups by name; an overridden composite key groups by the custom key.
- [x] The solution builds.

## Answer

`AddonSetCollectorBase` now has `protected virtual object GetDeduplicationKey(TAddon addon) => addon.Name;`
and `GetAvailableAddons` groups by it. The key is an `object` (compared by equality) so a collector can return a
composite key (a tuple/record) without delimiter-collision concerns.

Tests: `tests/LLMDesktopAssistant.Tests/Addons/AddonSetCollectorDeduplicationTests.cs` — the default key groups by
name (with overrides populated), the composite `(name, key)` key splits/groups correctly. Full suite: 788 passed,
1 skipped; main + desktop builds green.
