# NOTICE

This directory contains code derived from **Iciclecreek.Avalonia.Terminal**
(https://github.com/tomlm/Iciclecreek.Avalonia.Terminal),
Copyright (c) 2025 Tom Laird-McConnell, licensed under the MIT License.

The following files are copied from (or heavily based on) the original project,
with namespace renames and minor adjustments:

- `BufferCellExtensions.cs`
- `ProcessExitedEventArgs.cs`
- `TerminalExtensions.cs`
- `TerminalRenderThrottle.cs`
- `TerminalView.cs`
- `TitleChangedEventArgs.cs`
- `Win32ControlKeyState.cs`
- `WindowInfoRequestedEventArgs.cs`
- `WindowMovedEventArgs.cs`
- `WindowResizedEventArgs.cs`

## Modifications

- All types were moved from the `Iciclecreek.Terminal` / `Iciclecreek.Avalonia.Terminal`
  namespaces into `LLMDesktopAssistant.Desktop.Execution.Terminals`.
- `TerminalView` no longer manages the PTY process lifecycle (launch, reading, kill).
  Process creation, output pumping and termination are owned by
  `LLMDesktopAssistant.Desktop.Execution.ProcessLauncher` / `ProcessTerminalSession`;
  the view only renders the terminal buffer and forwards user input to the PTY.
- `TerminalControl` and `TerminalWindow` were not ported - they are not needed by
  this application.

## License

MIT License

Copyright (c) 2025 Tom Laird-McConnell

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
