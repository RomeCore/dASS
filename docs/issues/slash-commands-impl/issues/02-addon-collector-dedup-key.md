# 02: Add `GetDeduplicationKey` to `AddonSetCollectorBase`

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Make the addon-set deduplication key overridable so that a collector whose items are not unique by name alone
(the upcoming `SlashCommandSetCollector`, where names may collide across namespaces) can group by a
fully-qualified key, while every existing collector keeps grouping by `Name`.

## Acceptance criteria

- [ ] `AddonSetCollectorBase<TAddon, TChange>` exposes a `protected virtual` deduplication key (default: `AddonBase.Name`).
- [ ] `GetAvailableAddons` groups by that key instead of `GroupBy(s => s.Name)`.
- [ ] Existing collectors (`ToolSetCollector`, `SkillSetCollector`, `SubAgentSetCollector`, …) behave exactly as before.
- [ ] A unit test covers: default key groups by name; an overridden key groups by the custom key.
- [ ] The solution builds.
