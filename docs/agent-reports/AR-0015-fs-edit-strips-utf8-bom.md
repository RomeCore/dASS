# AR-0015 - fs-edit (and fs-apply_diff) silently drop the UTF-8 BOM from the file they edit

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `fs-edit`, tool `fs-apply_diff`
- **First seen:** 2026-10-18

## What I expected

The tools promise to apply patches to a text file: "Text before and after the match is preserved", "All other patches are still applied". Read as a contract, an edit changes exactly the characters that matched and nothing else. The file's bytes outside the patched span - encoding, BOM, line endings - are not part of the edit and should survive it.

The repository is full of files that carry a UTF-8 BOM: `.csproj` files, `.cs` files with Cyrillic strings, localization resources. A tool that edits source in this repo should leave the encoding alone.

## What happened

On a minimal file with a BOM and CRLF, `fs-edit` returns `File edited successfully` and a diff of the intended change. The bytes tell a different story.

**Before** (109 bytes, BOM `EF BB BF`, 5 x CRLF):

```
239,187,191,60,80,114 ...
```

**Patch:** `<Foo>old</Foo>` -> `<Foo>new</Foo>`

**After** (106 bytes, CRLF still 5, BOM gone):

```
60,80,114,111,106,101 ...
hasBOM = False
```

Exactly 3 bytes disappeared - the BOM. The diff the tool prints shows only the intended line, so nothing hints that the first line's bytes changed too.

`fs-apply_diff` behaves the same way: a file starting with `239,187,191` (`line1\r\nline2\r\nline3\r\n`) came back starting with `108,105,110,101,49,13` (`line1\r` ...) after inserting one line. CRLF was preserved in both tools, so this is not a newline normalisation - it is specifically the byte-order mark being dropped.

In the real session this showed up as a phantom hunk in `git diff` after a one-line edit of `src/LLMDesktopAssistant.Android/LLMDesktopAssistant.Android.csproj`:

```
-﻿<Project Sdk="Microsoft.NET.Sdk">
+<Project Sdk="Microsoft.NET.Sdk">
```

Restoring the file to its committed state (reverting the edit) did not remove that hunk - the BOM was already gone, and the working tree stayed dirty until the bytes were put back manually.

## Why it costs

- **The change is invisible in the tool's own output.** The reported diff is clean, the call succeeded, the file still parses - the damage is only visible by inspecting raw bytes or by reading a `git diff` that happens to include the first line.
- **It pollutes every subsequent diff.** Any commit touching the file now carries an unrelated encoding change in its first line, which a reviewer has to notice and consciously discard. In a repo where `.csproj` and localization files carry BOMs, this is a recurring tax.
- **It can break consumers on Windows.** PowerShell 5.1 and a number of legacy tools read a BOM-less UTF-8 file using the ANSI code page, so Cyrillic text that was fine before the edit turns into mojibake after it. The failure surfaces nowhere near the edit that caused it.
- **The recovery is manual and byte-level.** The only reliable repair from inside the session was `[System.IO.File]::WriteAllBytes` with `EF BB BF` prepended - well outside what "edit one line" should cost.

## Repro

Minimal, from a PowerShell prompt in the working directory:

```powershell
# 1. create a file with a UTF-8 BOM and CRLF
$bom  = [byte[]](0xEF,0xBB,0xBF)
$text = "<Project Sdk=`"Microsoft.NET.Sdk`">`r`n  <PropertyGroup>`r`n    <Foo>old</Foo>`r`n  </PropertyGroup>`r`n</Project>`r`n"
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) "bom.csproj"), ($bom + [System.Text.Encoding]::UTF8.GetBytes($text)))

# 2. fs-edit: match "<Foo>old</Foo>" -> "<Foo>new</Foo>"
#    (reports "File edited successfully", diff shows only the Foo line)

# 3. inspect the first bytes
$b = [System.IO.File]::ReadAllBytes("bom.csproj")
$b[0..2] -join ','                     # -> 60,80,114   (was 239,187,191)
```

Same sequence with `fs-apply_diff` (an insertion) reproduces it too.

## Witnesses

- **2026-10-18** - Android packaging session - `fs-edit` was used to revert two lines in `src/LLMDesktopAssistant.Android/LLMDesktopAssistant.Android.csproj`. After the edit, `git diff` still showed a BOM removal on line 1 even though the content matched `HEAD`; the file had to be rewritten byte-for-byte with a BOM prefix to make the diff clean.

## What I did instead

Re-added the BOM manually:

```powershell
$p = "src/LLMDesktopAssistant.Android/LLMDesktopAssistant.Android.csproj"
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $p))
if (-not ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) {
    [System.IO.File]::WriteAllBytes((Resolve-Path $p), [byte[]](0xEF,0xBB,0xBF) + $bytes)
}
git diff -- $p    # -> empty
```

That works, but it is a second, byte-level edit performed to undo an undeclared side effect of the first one.

## Proposed fix

Cheapest first:

1. **Preserve the BOM.** On read, note whether the file starts with `EF BB BF` (and whether it is UTF-16/UTF-32 with its own BOM); on write, emit the same encoding with the same preamble. A single `new UTF8Encoding(encoderShouldEmitUTF8Identifier: <detected>)` covers the common case.
2. **State it when it does change.** If the writer must normalise, say so in the success summary: `re-encoded as UTF-8 without BOM`. A one-line warning turns a silent corruption into a visible choice.
3. **Apply the same treatment to every file-writing tool** (`fs-edit`, `fs-apply_diff`, `fs-write_file`), since the cause is evidently shared - and keep line endings as they are today (both tools already preserved CRLF in the repro above).

## Unverified

- Whether `fs-write_file` (with `append: false`) also drops the BOM. Not tested in this session.
- Whether UTF-16 / UTF-16BE files (`FF FE`, `FE FF`) survive an edit with their encoding intact. Only UTF-8 BOM was reproduced.
- Why CRLF survived while the BOM did not: the writer probably round-trips the text through `string` (which keeps `\r\n` as characters) and re-encodes without a preamble. This is inferred from the observed bytes, not read from the implementation.
- Whether the BOM removal happens on every save or only when the matched text touches the first line. Both repros patched lines other than the first, and the BOM was removed in both, so it looks unconditional - but the implementation was not opened.
