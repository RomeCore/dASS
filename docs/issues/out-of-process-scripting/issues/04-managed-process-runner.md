# Managed process runner and result taxonomy

Status: done
Type: task
Blocked by: 01, 03

## Question

Implement `RunAsync` of the language engines (spec §7, §12):

- write the generated script to the reserved temp folder and delete it in `finally` - never into the working
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

The runner is one class - `ExternalProcessScriptEngineBase` - and it landed with the family: the temp script, the
timeouts, the `finally` invalidation, the channel split and the status taxonomy are shared by every language, and a
language engine supplies only `BuildScript`, `ScriptEncoding` and `CreateLaunchParameters`. What each of the four adds:

| Language | Interpreter | Arguments | Environment |
|---|---|---|---|
| Python | `PythonHelperService` discovery | `python "<script>"` | the addon venv activation |
| Bash | `IBashInterpreterLocator` | `"<script>"` | none |
| Batch | `IBatchInterpreterLocator` | `/d /s /c ""<script>""`, `VerbatimArguments` | none |
| PowerShell | `IPowerShellInterpreterLocator` | `-NoLogo -NoProfile -ExecutionPolicy Bypass -File "<script>"` | none |

Two language-specific facts worth recording:

- **Batch cannot be launched directly.** `CreateProcess` does not run a `.bat`, so cmd is the process: `/d` skips the
  machine-wide AutoRun, `/s` pins cmd's quote stripping to its documented "old" behaviour, and the doubled quote pair
  leaves one quoted path for cmd to run - a temp path with a space in it would otherwise be split.
- **PowerShell takes one argument list everywhere.** `-ExecutionPolicy` is documented as ignored off Windows, so
  there is no platform branch; `-NonInteractive` is deliberately absent, because a `run-terminal` script has to be able
  to prompt, and `-NoProfile` keeps a machine-wide alias from changing what an addon does. `-File` stays last: every
  character after it would become a script parameter.

### What changed on 2026-10-26

`BuildScript` moved **inside** the guarded `try` in both entry points. It used to be computed above it, so a builder
that refuses its own language's limits threw straight out of `RunAsync`; now the refusal is a
`ExternalProcessStatus.ProcessFailed` result with the message as `Error` (see ticket 03, D25).

The two locators follow the same contract as bash's: a missing interpreter is a `ExecutableResolution` with a
`Problem`, never an exception, because the parse is app-wide and the interpreter path is per-chat (D20). `Batch`
answers the same way for its own platform rule: off Windows there is no `cmd.exe`, and the descriptor stops claiming
`.bat`/`.cmd` at all (D29).

## Comments

- Landed with the family; the two new engines joined it on 2026-10-26 without changing a line of the runner except the
  `BuildScript` guard.
- Verified: `bat-echo` and `ps-echo` in the scratch pack ran through `ExternalProcessToolEngine` - the command
  processor came from `COMSPEC`, the PowerShell from discovery, both temp scripts landed in
  `%LOCALAPPDATA%\.llmassist\temp\scripts` and neither survived the call.
