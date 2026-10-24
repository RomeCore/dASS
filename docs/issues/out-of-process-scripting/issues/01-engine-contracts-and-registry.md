# Engine contracts, descriptors and the language registry

Status: open
Type: task
Blocked by:

## Question

Implement the contract layer of the family in `Desktop/Scripting/ExternalProcess/` (spec §4, §5):

- the enums `ScriptableAddonKind`, `ScriptInputMode`, `ScriptOutputMode`, `ExternalProcessStatus` and
  `ScriptEngineCapabilities`;
- `ExternalProcessScriptTerminalParameters`, `ExternalProcessScriptParameters`,
  `ExternalProcessScriptResult`, `ExternalProcessScriptTerminalResult` exactly as approved in spec §4, plus the
  terminal `Wait` flag;
- `IExternalProcessScriptEngine` and `IScriptEngineDescriptor` (the descriptor interface lives in core because
  core parsers read it);
- `Descriptor` → `IReadOnlyList<IScriptEngineDescriptor> Descriptors` on `IScriptableToolEngine`,
  `IScriptableSlashCommandEngine` and `IScriptablePromptContextEngine`; delete `Language` from those three (it
  duplicated the descriptor);
- update the three file locators and the three parsers to build the extension → descriptor map from
  `Descriptors`, and to set `ScriptLanguage` on the parsed addon from the descriptor;
- the Desktop registry the adapters use to resolve a descriptor and an engine by file path;
- keep the three Python engines compiling on the new shape as a temporary bridge — they are deleted in the
  ticket "Kind adapters and addon flags", and nothing new may depend on them.

## Answer

## Comments

- **Contract half landed** on 2026-10-24 (uncommitted when this note was written). What is in: `IScriptEngineDescriptor` (extensions, frontmatter markers, `FrontmatterLinePrefix`, `Capabilities`) + `ScriptEngineCapabilities` in `Scripting/`; `Descriptor` → `IReadOnlyList<IScriptEngineDescriptor> Descriptors` on all three kind interfaces; `Language` removed from them (the descriptor is now the only source, and the parsers set `ToolInfo.ScriptLanguage` from it); the three kind-specific descriptor interfaces are deleted; the three parsers resolve extension → (engine, descriptor) and pass `FrontmatterLinePrefix` into `AddonParserDescriptor`; the three locators collect extensions from the descriptor lists. Lua declares `StructuredInput | StructuredOutput`, Python adds `Terminal`.
- **The Desktop language registry moved to the adapters ticket** (08): its only consumer is the adapter, and its only denizens are `IExternalProcessScriptEngine`s that do not exist until then. Nothing in this slice needs it.
- The `FrontmatterLinePrefix` support in `FrontmatterBasedAddonParser` predates this slice; the slice only wires descriptors to it — every parser now passes `descriptor.FrontmatterLinePrefix` into `AddonParserDescriptor`.
- Builds: core and desktop OK; core tests 1273 passed, 1 pre-existing skip.
