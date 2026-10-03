# 2026-10-04 - Lua tools render their own dynamic AXAML UI (dass.ui.create_control + append_data + DynamicAxamlControl*), plus AsyncLua task.create()

## Signals

- **Multi-step manual ritual** → the dynamic-UI Lua contract had to be reconstructed from five files (`LuaApiUi`, `LuaUiControl`, `DynamicAxamlControl`, `DynamicAxamlControlAdditionalData`, `LuaStructuredConverter`) → skill `lua-ui-controls`.
- **A tool call needed a second attempt** → `shell-powershell` mangled double quotes in the command argument → report `dass-report` AR-0002, not equipment.
- **A tool call failed** → `shell-powershell` returned non-ASCII console output as mojibake → report AR-0003, not equipment.
- **"No way to do this"** → a skill instructs the agent to use `time-get`, which is absent from the agent's toolset → report AR-0004, not equipment.
- **The same command twice in one session** → `dotnet build '…' | Select-Object -Last N` ×3 and one `dotnet test --filter` → `dotnet` build/test tool candidate → **killed**: the same candidate was refused on 2026-09-30 as a duplication of `AGENTS.md`.
- **"No way to do this"** → `task.create()` did not exist → we added it in the AsyncLua source; a capability gap closed by code, not by an addon.
- **The same output shape hand-assembled again** → the build/test summary ritual → folded into the `dotnet` candidate, killed above.
- **The same external fact fetched twice** → none this session.
- **A source that could not be reached at all** → none this session.

## Forged

- `lua-ui-controls` - `.agents/skills/lua-ui-controls/SKILL.md` - authoring a Lua tool that renders custom dynamic UI stops being re-derived from five source files: the pack-then-append handle semantics (`dass.ui.create_control` → `dass.tool.result.append_data`), the reflection-binding markup, the `set`/`notify`/`__changed` view-model contract, and the BSON snapshot (dropped Lua functions, top-level-only persistence, dead commands after reload).

## Refused / deferred

- `dotnet` build/test tool - **refused (again)**: already documented in `AGENTS.md`, and refused on 2026-09-30. This session's extra cost was the shell transport (quoting, encoding), which belongs in `dass-report` (AR-0002/AR-0003), not in a new tool.
- `time-get` tool - **refused**: it exists natively (`src/LLMDesktopAssistant/Tools/Implementations/TimeToolModule.cs`). The real gap is agent-toolset exposure, filed as AR-0004.
- `shell-powershell` quoting and encoding - **refused as equipment**: native tool contract issues, filed as AR-0002 and AR-0003.
- "Do not over-explore the codebase" steering - **refused**: steering sits outside equip by decision (same call as the 2026-09-30 note).
- Skill "verify AsyncLua source changes" (edit the AsyncLua repo → build → test → republish the NuGet) - **deferred**: low frequency, and the running runtime already reports AsyncLua 0.5.2.
