# 2026-09-30 - SCM section `working-directories` + prompt contexts in settings; the anchor discriminators turned out to be unpersisted

Session ended with commit `3a233686` (34 files) on top of the user's `8bf93d4a` (core_prompt.llt split) and `285b74a6` (availability predicates).

## Signals

- **Multi-step manual ritual** -> scaffolding an SCM section: 10 files by template plus registration, template file, locale keys and settings wiring -> skill `prompt-section-authoring`.
- **The same query twice in one session** -> the locale key audit (`Locale.Get*` / `{loc:Loc ...}` usages versus the `.loc` definitions) was done by hand twice -> tool `locale-audit`.
- **Multi-step manual ritual + "no way to do this"** -> splitting `core_prompt.llt` at `@template` boundaries with a reassembly self-check, and no way to validate `.llt` without starting the app -> tool `llt-templates`.
- **A tool call needed a second attempt** -> `fs-rename_file` and `fs-edit` on the same path in one parallel batch: the edit executed against the old path and was lost -> report, not equipment.
- **"No way to do this"** -> no way to inspect the chat LiteDB to test the hypothesis about the lost anchor states -> deferred; the incident was settled by reasoning about the mapper.
- **The same output shape hand-assembled again** -> the iv + ru-RU locale pairs were written by hand and `addon.type.context.*` was missed -> folded into `locale-audit`.

## Forged

- `prompt-section-authoring` - `.agents/packs/authoring/skills/prompt-section-authoring/SKILL.md` - the SCM section contract (quartet, provider registration, order registry, the `Resources/sections` template file, delta semantics including `State = null`, and the anchor pitfalls) stops being re-derived from the code every session. New pack `authoring` holds skills about editing specific code elements, next to the general code/runtime skills in `.agents/skills/`.

## Refused / deferred

- `locale-audit` (Lua tool, **S**) - deferred, not refused: a real signal (audited by hand twice, `addon.type.context.*` missed by the same routine), waiting for the user's pick.
- `llt-templates` (Lua tool, **M**) - deferred: list / split / extract / sanity-check `@template` blocks. Covers both the split ritual and the "cannot validate templates without running the app" gap.
- The `fs-rename_file` + `fs-edit` race on one path - refused as equipment: it is a native tooling contract issue and belongs in `dass-report`. The environment applies sibling calls in parallel, so a rename must never share a batch with an edit of that path.
- LiteDB dropping non-public members - refused as equipment: a framework pitfall, now recorded inside `prompt-section-authoring` (anchor pitfall 2). The code fix already landed.
- "Do not spam sub-agents" (three parallel `agent-call`s were cancelled and annoyed the user) - refused: a steering preference, and steering sits outside equip by decision.
- The `dotnet build` ritual (main project, then the desktop project with the temp artifacts path) - refused as duplication: already documented in `AGENTS.md`.
- A chat DB / anchor inspector - deferred: a debugging surface with low frequency, and the incident was solved without it.
