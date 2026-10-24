# Terminal run path

Status: done
Type: task
Blocked by: 04

## Question

Implement `RunInTerminalAsync` and the terminal mode of the tool and command adapters (spec §9):

- extract the launch / await / kill part of `TerminalBasedToolModule.Run` into a shared `TerminalRun` and reuse
  it in both places — no second copy of the cancellation logic;
- launch with `WithTimeout(null)` (`ProcessLaunchParameters.TimeOut` defaults to 30 minutes) and
  `CancellationToken.None`, registering the kill manually, so a stopped generation or a long session does not
  kill an interactive process;
- create `TerminalAdditionalViewModel`, attach it to the `IChatAttachmentHost` passed in, and never allow Leave;
- honour `Wait`: `true` awaits the exit and returns the whole `Output`; `false` returns immediately with
  `Status = Succeeded`, `ExitCode = null` and an output snapshot, completing the card in the background;
- `run-terminal` + `json-output` is a fatal addon diagnostic (D11).

## Answer

Landed on 2026-10-25: `TerminalRun` (`Desktop/Execution/`) now owns attach → wait/leave → kill → background exit
report, and `TerminalBasedToolModule.Run` is a thin caller of it. `ExternalProcessScriptEngineBase.RunInTerminalAsync`
writes the same script as the managed path, launches with `runInTerminal: true` + `WithTimeout(null)` +
`CancellationToken.None`, attaches the terminal to the `IChatAttachmentHost` **with `IsLeaveAllowed = false`** (a
detached script has nowhere to deliver its result; the fire-and-forget case is `wait: false`) and maps the outcome
(`Left` → `Succeeded` with the detach text, no exit code → `Succeeded` snapshot, non-zero → `ProcessFailed`,
cancellation → `Cancelled`). `run-terminal` in the tool/command adapters passes `TerminalWait`.

Python is the only language that has a terminal today; the other three arrive with their engines. Manual
verification is required (an interactive PTY cannot be exercised by a unit test).

## Comments
