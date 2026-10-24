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
