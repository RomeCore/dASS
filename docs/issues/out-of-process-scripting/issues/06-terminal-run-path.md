# Terminal run path

Status: open
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

## Comments
