# Managed process runner and result taxonomy

Status: open
Type: task
Blocked by: 01, 03

## Question

Implement `RunAsync` of the language engines (spec §7, §12):

- write the generated script to the reserved temp folder and delete it in `finally` — never into the working
  directory;
- launch through `IProcessLauncher` with the language's interpreter, the chat's working directory, the venv
  environment where the language has one, and the per-kind timeout (tool 30 min, command 10 min, context from
  its own budget);
- resolve exactly two chat-scoped services: `IChatSettingsService` and `IAddonManagerInvalidator`; invalidate
  addons in `finally` for tools and commands, never for contexts (D19);
- fill `ExternalProcessScriptResult`: `Output` verbatim from `ProcessDescriptor.Output` (the launcher already
  merges the streams in arrival order), `Stdout`/`Stderr` from the split channels, `Status` from the exit code
  and the cancellation, `Error` from stderr with the exit-code fallback when stderr is empty;
- map `OperationCanceledException` to `TimedOut` or `Cancelled` depending on which token fired.

## Answer

## Comments
