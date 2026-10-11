# AR-0008 - AGENTS.md's desktop-build command fails in PowerShell (`$env:TEMP` inside single quotes stays literal)

- **Status:** fixed
- **Severity:** friction
- **Lane:** agent
- **Surface:** docs (`AGENTS.md`, "Building project")
- **First seen:** 2026-10-12

## What I expected

The "Building project" section says the desktop project "should be built inside the temporary directory" and gives a
`powershell` fenced block. Read as a contract, copy-pasting that block should build the desktop project into the temp
directory.

## What happened

The command as written

```
dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath='$env:TEMP\dass-temp-build\'
```

failed with a NuGet error, because PowerShell single quotes are literal: `$env:TEMP` was **not** expanded, and the
literal text landed in the artifacts path.

```
...\NuGet.targets(198,5): error Неверно задано имя папки. :
'E:\CSharp Projects\LLMDesktopAssistant\src\LLMDesktopAssistant.SourceGenerators\$env:TEMP\dass-temp-build\obj\LLMDesktopAssistant.SourceGenerators'.
```

Computing the path first (double quotes expand) worked on the first try:

```
$a = Join-Path $env:TEMP 'dass-temp-build'; dotnet build '...Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath="$a"
```

## Why it costs

- A whole build round-trip (and its restore) is wasted before the cause is understood.
- The failure surfaces as a NuGet "invalid folder name" pointing at a `LLMDesktopAssistant.SourceGenerators` path - it
  never says "your variable was not expanded", so the natural first reaction is to suspect the build configuration,
  not the documented command.
- This is a *documented* incantation meant to be copy-pasted on every desktop build, so the cost recurs every session
  that touches the desktop project.

## Repro

1. In PowerShell, run the `AGENTS.md` block verbatim:
   `dotnet build 'src/LLMDesktopAssistant.Desktop/LLMDesktopAssistant.Desktop.csproj' -p:UseArtifactsOutput=true -p:ArtifactsPath='$env:TEMP\dass-temp-build\'`
2. Observe the NuGet folder-name error containing a literal `$env:TEMP` path segment.

## Witnesses

- **2026-10-12** - session that implemented scriptable slash commands and the Lua chat API (`dass.chat` / `dass.command`)
  - first desktop build after the change, following the AGENTS.md block verbatim; the literal-`$env:TEMP` error was
    returned, then the variable-based form succeeded.

## What I did instead

Built the artifacts path into a variable first and passed it in double quotes
(`$a = Join-Path $env:TEMP 'dass-temp-build'; ... -p:ArtifactsPath="$a"`).

## Proposed fix

Cheapest first:

1. Update the `AGENTS.md` block to a form that works in PowerShell, e.g. compute the path first
   (`$a = Join-Path $env:TEMP 'dass-temp-build'`) and pass `-p:ArtifactsPath="$a"`, or use
   `-p:ArtifactsPath="$env:TEMP\dass-temp-build"` with **double** quotes.
2. Related but not the same (see AR-0002): `shell-powershell` mangles double-quoted *arguments*, which is the likely
   reason the doc reached for single quotes. The trap-free pattern is single quotes for literal tokens, double quotes
   (in a variable) where expansion is needed - so the doc should not try to expand inside single quotes at all.

## Unverified

- Whether the trailing separator in the original command matters, or `UseArtifactsOutput` alone suffices, was not
  checked.
- Not checked on non-PowerShell shells (the section is a `powershell` block, so only PowerShell was exercised).
