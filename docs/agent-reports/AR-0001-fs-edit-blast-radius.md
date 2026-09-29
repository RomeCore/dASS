# AR-0001 - fs-edit gives no way to know, before writing, how much of the file a patch will touch

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `fs-edit`
- **First seen:** 2026-09-29

## What I expected

The tool's description says each patch replaces **ALL** occurrences of its `match`, that leading/trailing whitespace and common indentation are ignored when matching, and that text before and after the match is preserved.

Read as a contract, that promises something computable: given a `match`, I can work out exactly which characters are going to change, and therefore decide in advance whether the patch is safe to send.

## What happened

I could not work it out, and the tool offers no way to check before the write. Two gaps, both visible from the description alone:

1. **No preview of the blast radius.** The patch is applied, and only then does `[APPLIED CHANGES]` arrive with a diff. There is no dry run, no "matched N occurrences", no way to ask "what would this touch?". The first evidence about how much of the file moved arrives *after* the file has moved.
2. **The indentation rule covers matching, not writing.** "Common indentation is ignored in matching" settles which text is *found*. It says nothing about what happens to the replacement's leading whitespace when the matched text carried indentation of its own. The documented example (`match: "public class Foo"` matching `"    public class Foo"`) demonstrates lenient matching and leaves the resulting line unspecified.

## Why it costs

- **Silent damage is available by construction.** A `match` short enough to be convenient can hit other occurrences *elsewhere in the file*. The file still parses, still reads plausibly, and the tool reports success. Nothing raises, and the diff - the only evidence - is a wall of hunks that is easy to skim past.
- **Every multi-line or whitespace-carrying patch costs a deliberation cycle.** Attention goes into guessing rules that cannot be verified from where the model sits, instead of into the work.

## Repro

Any file with two identical lines in two different contexts:

```
1. write a file containing the line "value: 1" twice, in two unrelated sections
2. fs-edit with match "value: 1", replace "value: 2"
3. both sections change, and the tool reports success
```

## Witnesses

- **2026-09-29** - session that built the `equip` skill - one intended edit, the same line (`- file_read` → `- file-read`) inside the `Template` of three separate engine descriptors: `LuaToolEngineDescriptor.cs`, `CSharpScriptToolEngineDescriptor.cs`, `PythonToolEngineDescriptor.cs`. Because "ALL occurrences" makes the reach of any convenient `match` unknowable before the write, one call was not available: three single-file calls were made, each with the shortest possible match (the bare line, no indentation) - the safest guess on offer. It worked. A guess that happens to work is not a contract.

## What I did instead

Split one intended edit into three calls, and shortened every match to the bare line so it could not span anything unintended. Workable - and it rules out any edit whose target text is not unique in its file, which is most edits worth making.

## Proposed fix

1. **A dry run.** `dryRun: true`, or a `preview` action, returning the match count and the line numbers that would change, without touching the file. Cheapest option, and it closes the whole class.
2. **Report "N occurrences replaced"** in the success summary. One number, and a multi-match patch stops being invisible.
3. **One sentence that covers the write, not just the match.** Either "the replacement is inserted verbatim" or "the replacement inherits the matched line's indentation" - today's wording settles the search and leaves the write ambiguous.

## Unverified

- No file has been corrupted by this yet, and no skipped patch was observed, so how loud a skipped patch is in the result is unknown from this session.
- Everything above is read from behaviour plus the tool's own description. The `fs-edit` implementation was not opened - pinning the cause means leaving whatever repo the session is working in, which is a different job from this report.
