# 01: Rename `AddonKind.Command` to `SlashCommand`

Status: resolved
Type: task
Blocked by:

## What to build

Activate the reserved `AddonKind.Command` member by renaming it to `SlashCommand` and refreshing its documentation,
so the command addon kind (planned in Stage 1) sits on a real, non-reserved enum member.

Scope is the enum only — do **not** add an `IAddonTypeDescriptor`, do **not** extend `AddonPathImpactDetector`
(it already scans every locator's configuration), and do not create any settings MVVM.

## Acceptance criteria

- [x] `AddonKind.Command` is renamed to `AddonKind.SlashCommand` (value `1 << 6` unchanged).
- [x] `AddonKind.All` includes `SlashCommand`.
- [x] The XML doc comment on the member no longer says "reserved for future use".
- [x] No reference to `AddonKind.Command` remains anywhere in the repo (grep clean).
- [x] The solution builds (main project + desktop project).

## Answer

Done in `AddonKind.cs`: `Command = 1 << 6` → `SlashCommand = 1 << 6`, added to `All`, dropped the
"reserved" TODO line, refreshed the `All` doc comment. No other references existed. Main + desktop builds green.
