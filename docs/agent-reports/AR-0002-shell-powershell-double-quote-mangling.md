# AR-0002 - shell-powershell mangles double quotes inside the command argument

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** tool `shell-powershell`
- **First seen:** 2026-10-04

## What I expected

The description says the tool "executes WINDOWS POWERSHELL command or script from the current working directory" and gives examples like `git status`. Read as a contract, a `powershell` string containing double-quoted literals (a file path, a filter) should be executed as that PowerShell command, with those literals intact.

## What happened

A command containing `"..."` was rejected by the PowerShell parser before anything ran:

```
В строке отсутствует завершающий символ: ".
+ CategoryInfo          : ParserError: ...
TerminatorExpectedAtEndOfString
```

I hit this twice - once with the full pipeline (`... 2>&1 | Select-Object -Last 40`), once after removing the pipeline - both with double-quoted arguments. The *identical* command rewritten with single quotes ran correctly on the first try.

- Failed: `dotnet test "E:\CSharp Projects\AsyncLua\tests\AsyncLua.Tests\AsyncLua.Tests.csproj" --filter "FullyQualifiedName~TaskLibraryTests" --nologo`
- Worked: `dotnet test 'E:\CSharp Projects\AsyncLua\tests\AsyncLua.Tests\AsyncLua.Tests.csproj' --filter 'FullyQualifiedName~TaskLibraryTests' --nologo`

## Why it costs

- Each double-quoted command is a failed round-trip: the error is a parse failure, so nothing ran and the whole call is wasted - two calls were burned before switching to single quotes.
- The failure is not attributed to quoting anywhere in the result, so the natural first reaction is to suspect the command itself, not the transport.
- It pushes every caller onto single-quote-only commands, which is workable until a value legitimately needs to contain a double quote (`"`), where the transport offers no known-correct form.

## Repro

1. Call `shell-powershell` with `powershell` = `Write-Output "hello world"`.
2. Call it again with `powershell` = `Write-Output 'hello world'`.

Observed in this session: the double-quoted form produced the parser error above; the single-quoted form executed.

## Witnesses

- **2026-10-04** - session that added the Lua-UI APIs and `task.create()` - running the AsyncLua test suite via `dotnet test`; two consecutive double-quoted attempts failed, the single-quoted attempt succeeded.

## What I did instead

Rewrote every command with single quotes and kept using them for the rest of the session.

## Proposed fix

1. **State the embedding rule in the description.** Either "the string is passed to PowerShell verbatim" (then the transport must preserve `"`) or "the string is embedded into a generated script; quote for the generated layer" - today's wording says neither, and the examples only ever use quote-free commands.
2. **Make the transport preserve the argument.** Whatever layer wraps the string in its own quotes should either avoid re-wrapping or escape the inner quotes.
3. **Name the culprit in the error text** ("argument was passed as: ...") so a parse failure points at the transport and not at the command.

## Unverified

- The tool implementation was not inspected; which layer re-writes the quotes (JSON argument handling, script wrapper, or the embedded-terminal path) is unknown.
- Not confirmed whether the same mangling affects escaping/backticks or only double quotes.
