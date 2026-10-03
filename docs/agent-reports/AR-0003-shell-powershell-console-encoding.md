# AR-0003 - shell-powershell returns mojibake for non-ASCII console output

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `shell-powershell`
- **First seen:** 2026-10-04

## What I expected

The description says the tool executes a command and (with `runTerminal`) shows its output. Read as a contract, text output should come back readable - in particular the localized summary that `dotnet build` / `dotnet test` print on a Russian Windows.

## What happened

Every build/test invocation returned Cyrillic as mojibake. The success summary arrived as:

```
╨б╨▒╨╛╤А╨║╨░ ╤Г╤Б╨┐╨╡╤И╨╜╨╛ ╨╖╨░╨▓╨╡╤А╤И╨╡╨╜╨░.
    ╨Я╤А╨µ╨┤╤Г╨┐╤А╨╡╨╢╨┤╨╡╨╜╨╕╨╣: 0
    ╨Ю╤И╨╕╨▒╨╛╨║: 0
```

instead of "Сборка успешно завершена. / Предупреждений: 0 / Ошибок: 0". The ASCII parts (paths, flags, `0`) survived; the localized words did not - consistent with the child process writing one code page and the captured/displayed text being decoded as another (UTF-8 bytes read as cp866, or vice versa). Both the returned tool text and the embedded terminal were affected. The same garbling recurred across ~5 `dotnet build` / `dotnet test` invocations.

## Why it costs

- The one-line status (success, warning count, error count) is exactly the part that is localized and exactly the part that is unreadable. The agent must fall back to digits, exit codes or `Select-Object -Last N`, or re-run just to read a word.
- A localized *diagnostic* (not just the summary) would be unreadable too, so a subtle warning can be misread as noise - a correctness risk, not only a cosmetic one.
- It is repeated on every command that prints localized text, i.e. essentially every toolchain command on a non-English Windows.

## Repro

1. On a Russian-locale Windows, call `shell-powershell` with `powershell` = `dotnet build 'src/LLMDesktopAssistant/LLMDesktopAssistant.csproj' -v quiet --nologo`.
2. Read the summary line - Cyrillic comes back as mojibake.

## Witnesses

- **2026-10-04** - session that added the Lua-UI APIs and `task.create()` - five `dotnet build` / `dotnet test` invocations, each showing the garbled summary.

## What I did instead

Treated the output as unreadable for prose and relied on the ASCII numbers and the exit code; piped through `Select-Object -Last N` to reduce volume.

## Proposed fix

1. **Decode captured output with the child's actual code page.** Set the encoding explicitly for the child process (e.g. force UTF-8 in the spawned shell via `[Console]::OutputEncoding` / `$OutputEncoding`, or decode using the detected OEM/ANSI code page) instead of assuming UTF-8.
2. **Expose the encoding as a parameter** (`encoding: "utf-8" | "oem" | "auto"`) so a caller can correct it without code changes.
3. **Same for the embedded terminal** - if it renders independently, it needs the matching font/codepage too.

## Unverified

- Which layer loses the encoding - the tool's process capture, the embedded terminal emulator, or the transfer back to the model - was not determined.
- The exact code page pairing (UTF-8 text decoded as cp866 vs cp1251 bytes) was not confirmed; only the resulting mojibake shape was observed.
