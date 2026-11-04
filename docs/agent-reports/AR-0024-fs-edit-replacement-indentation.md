# AR-0024 - `fs-edit` rewrites the indentation of the replacement's continuation lines unpredictably

- **Status:** fixed (2026-11-05)
- **Fixed:** 2026-10-25 - one rule instead of three: the first line of `replace` lands on the match and every following line moves by a single shift read from the second line, so a single-line and a multi-line `match` no longer behave differently. Known limit, now stated in the tool description: with a one-line `match`, "one level deeper than the first line" is byte-identical to "the file's own columns", so relative continuation indentation collapses onto the match level - a patch that opens a nesting level has to spell the columns out.
- **Reopened:** 2026-11-04 - the rule does not hold for multi-line matches. In one edit pass, five multi-line `match` patches moved the replacement's continuation lines anyway: three collapsed to column 0, one gained a level, and one repair patch damaged a block it had just written. The one-line-match limit does not explain any of them.
- **Fixed:** 2026-11-05 - `GetShift` no longer pairs line j of `replace` with the line it happened to replace. A patch copied out of the file lands verbatim; a patch that re-nests the matched block is read as relative to the match. The number of lines in `match` stops deciding the result, and the rule is stated in the tool description. See _Fix 2026-11-05_ below.
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `fs-edit`
- **First seen:** 2026-10-25

## What I expected

The description says: "Leading/trailing whitespace and common indentation are ignored in matching: `match: "public class Foo"` matches `"    public class Foo"` in the file. Text before and after the match is preserved."

The plain reading is that the indentation story is settled by that one sentence: matching is lenient, **writing is verbatim** - the characters I put in `replace` land in the file, and the indentation that preceded the match stays where it was as the prefix of the first replacement line.

Under that reading a multi-line replacement is computable in advance: write the continuation lines with the indentation you want them to have, and they will have it.

## What happened

Two calls in one session, two different behaviours - and only one of them is the documented one.

### Call 1 - a single-line `match`, replacement lines carrying their own tabs

I inserted one property line above an existing initializer line in a C# file. The target line sits at four tabs.

```
match:   Name = "fs-get_file_info",
replace: SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzePathRead,
         \t\t\t\tName = "fs-get_file_info",
```

What I expected: the new line at four tabs, the original line unchanged at four tabs.

What the tool echoed back and wrote:

```
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzePathRead,
								Name = "fs-get_file_info",
```

The first line is right (the preserved prefix did its job). The second line came out at **eight** tabs - the match's four-tab indentation was added on top of the four tabs I wrote. The result is valid C# with absurd indentation: nothing raises, and the file still compiles.

The same shape repeated on three more files in that session, every one over-indented by exactly the match's indentation.

### Call 2 - a multi-line `match`, continuation lines with relative indentation

Then I changed a property body, this time with a two-line `match` whose lines sit at two and three tabs.

```
match:   public bool CanCancel =>
         \t\t\t!ToolCall.Status.IsTerminal() && ...;
replace: public bool CanCancel =>
         \t!ToolCall.Status.IsTerminal() && ...;        (one tab - meant as "one deeper than the match")
         (blank)
         /// <summary>                                   (no indentation - meant as "same level as the match")
         ...
```

What I expected under the first call's behaviour: the match's two-tab indentation added to every continuation line, so the members end up at two tabs and the body line at three. That is what the same tool did one call earlier.

What actually landed:

```
		public bool CanCancel =>
	!ToolCall.Status.IsTerminal() && ...;

/// <summary>
/// Whether the call is queued behind another one ...
/// </summary>
public bool CanSkipSynchronizationWait => ...;
```

Verbatim. The body line kept its single tab, the doc comments and the new members landed at **column 0** - outside the class, which does not compile. Nothing in the replacement was re-indented at all.

### The rule I could not work out

- single-line `match` -> the match's indentation is **added** to the continuation lines of `replace`;
- multi-line `match` -> the match's indentation is **not** added.

My replacement's own whitespace is never stripped in either case, so the two behaviours are not two views of one rule. I could not derive which one applies without having already sent the call.

### The 2026-11-04 sighting - multi-line matches still do not follow the documented rule

The fix note claims one shift read from the second line, with the only known limit being a one-line `match`. Every damaged call in this session had a **multi-line** `match`. Four examples from a single implementation pass:

1. `src/LLMDesktopAssistant/Addons/AddonBase.cs` - a two-line `match` replacing a doc-comment line. Both `match` lines sit at two tabs, the replacement's second line was written at two tabs; it landed at column 0 (`/// </summary>`), so the doc comment closed outside the member.

2. `src/LLMDesktopAssistant/LLM/Settings/ChatCommandSettings.cs` - a multi-line `match` covering the class declaration and the gate property. The replacement wrote `\t\t{` and `\t\t\tget;`; the file received `{` and `get;` at column 0. A follow-up repair patch collapsed `get;` a second time, so the block took three calls to format.

3. `src/LLMDesktopAssistant/LLM/Services/Prompting/AgentPromptComposer.cs` - a multi-line `match` removing a constructor parameter. The replacement's next constructor line was written at two tabs and landed at column 0, leaving the primary-constructor parameter list visually outside the class body.

4. `src/LLMDesktopAssistant/SlashCommands/Providers/SubAgentCommandExecutor.cs` - a multi-line `match` where the replacement's continuation line was written at two tabs and arrived as `I\t\tIAgentTaskExecutor agentTaskExecutor,`. The preserved `I` is correct lenient matching; the two tabs inserted after it are the shift again.

5. `tests/LLMDesktopAssistant.Tests/SlashCommands/SlashCommandSetCollectorTests.cs` - the opposite direction: a replacement block written at two levels landed one level deeper, so the nested `GetService` body sat four tabs under its class instead of three.

The direction is not even consistent within one pass: cases 1-3 collapse, case 5 adds. That matches the original complaint, not the fixed note.

## Why it costs

- **It produces silent breakage in review, not in build.** Over-indentation compiles; column 0 does not. So one variant ships quietly and the other fails loudly later - and which one you get depends on an invisible property of the `match`.
- **Every multi-line insertion needs a verification pass.** Either I open the file afterwards, or I trust a tool that just did the opposite of what the same tool did a minute ago. In this session that was five repair calls (`fs-apply_diff` per damaged region plus two regex passes over a whole file) and roughly eight wasted `fs-edit` attempts.
- **It pushes the work onto a worse tool.** The only reliable way to insert a block turned out to be `fs-apply_diff` with explicit line numbers, which means counting lines in a 400-line file - exactly the bookkeeping `fs-edit` exists to avoid.
- **The 2026-11-04 pass added re-reads.** Each collapsed block had to be read back before it could be repaired, and two repairs were themselves mangled, so the same member was touched three times.

## Repro

```
1. Create a file, or take any line that sits at four tabs, e.g.
     				Executor = Something,
2. fs-edit with
     match:   Executor = Something,
     replace: SyncAnalyzer = SomeAnalyzer.Analyze,
              \t\t\t\tExecutor = Something,
3. The second line is written at eight tabs.

4. Now take a two-line block whose lines sit at two and three tabs, and send a multi-line `match`
   with a `replace` whose continuation lines carry one tab (or none).
5. Those lines are written exactly as typed - one tab, or nothing - with no re-indentation.

6. Minimal multi-line case: two `match` lines at two tabs, a `replace` whose second line is also
   at two tabs. The second line lands at column 0.
```

## Witnesses

- **2026-10-25** - session adding synchronization analyzers to the fs tools - eleven `fs-edit` calls inserting a `SyncAnalyzer` line above a `Name = "..."` line across five modules, all over-indented by the match's indentation and all repaired by a regex pass; then a multi-line edit of `ToolCallViewModel.cs` whose continuation lines landed at column 0, repaired with `fs-apply_diff`. The user interrupted mid-session to point the behaviour out ("fs-edit works with RELATIVE indents"), which is the strongest evidence that the intended rule is not the one the tool follows.
- **2026-11-04** - session implementing the reactive addon set cache - one pass over `AddonBase.cs`, `ChatCommandSettings.cs`, `AgentPromptComposer.cs`, `SubAgentCommandExecutor.cs` and `SlashCommandSetCollectorTests.cs`: four collapsed or shifted continuation lines and one over-indented block, each repaired with a follow-up `fs-edit` or `fs-apply_diff`. Two repair calls returned `No changes were made` while also reporting `1 occurrence replaced`, which cost an extra read to decide whether the file had changed.

## What I did instead

- For insertions: `fs-apply_diff` with `insertBeforeLine` + `insertText`, which writes the text exactly as given (and, to its credit, is the reason the C# was repairable at all).
- For bulk, mechanical changes: a Python script over the file with a regex, bypassing the tool entirely.
- For the indentation already damaged: a regex patch that rewrote every over-indented line back to four tabs.
- In the 2026-11-04 pass: re-read the damaged block, then either a one-line `fs-edit` per line or `fs-apply_diff` with absolute line numbers.

That is three different mechanisms replacing one edit, and the only one that behaved predictably was not a tool at all.

## Proposed fix

1. **Pick one rule and state it in the description.** Cheapest and decides everything else: either "the replacement is inserted verbatim - indentation included" or "the replacement inherits the matched indentation and its own leading whitespace is stripped". Today's wording describes matching and leaves writing open, and the implementation does both at once depending on the match's shape.
2. **Make the two shapes agree.** Whatever the rule, a single-line and a multi-line `match` of the same block must produce the same bytes. This is the actual bug: the same logical edit behaves differently depending on whether I put a line break in `match`.
3. **Treat a multi-line `match` as verbatim.** The 2026-10-25 note claims the shift is read from the second matched line; the 2026-11-04 cases show the shift still applied (or dropped) against that line. A regression test with a two-line `match` at two tabs and a `replace` whose second line is also at two tabs would settle it in one run.
4. **Add a way to say "verbatim"** (`replaceVerbatim: true`) if the inherited-indentation rule is kept for the common single-line case. Then a multi-line block edit is expressible without guessing.
5. **Echo the replacement's indentation in the diff**, e.g. a whitespace-visible mode, so the first evidence is not "read the file and count tabs".
6. **Make the result message truthful.** `No changes were made` and `1 occurrence replaced` must not be emitted for the same patch.

## Unverified

- The cause was not traced: the `fs-edit` implementation was not opened, and this session was working in the dASS repo, not in the tool's own code. What is observed is the input, the echo and the resulting bytes - not the branch that produced them.
- Whether `fs-apply_diff` inherits the same behaviour for multi-line `insertText` was not exercised deeply: every `insertText` in this session was written with absolute tabs and landed as written, which suggests it is verbatim, but a single counter-example would overturn that.
- The exact mechanism behind "single-line match re-indents, multi-line match does not" is guessed, not measured; a table of `match` shapes against their output would settle it in one run.
- The 2026-10-25 fix note says the behaviour was changed; whether the 2026-11-04 damage is a regression of that change or the same known one-line limit leaking into multi-line matches was not determined. - Settled by the 2026-11-05 fix: neither. The pairing of line j of `replace` with line j of the file survived that change and simply mispaired a patch whose second line had moved.

## Fix 2026-11-05

**The rule now.** One shift for the whole replacement, and the shape of the match no longer decides it.

1. If the match's second line sits at the same depth as the second matched file line, the patch was
   copied out of the file: line two of `replace` takes the place of line two of the match and the
   shift is their difference. A copied patch therefore lands verbatim - call 1 no longer doubles.
2. Otherwise the patch re-nested the matched block (a brace, a wrapper, a body line that moved a
   level): the shift is the depth of the first matched file line minus the depth of the match's own
   first line, so the block is relative to the match and every line keeps its offset from it. The
   wrap and the unwrap from the repro now land one level deeper and one level shallower rather than
   the reverse.

Both shapes of `match` go through the same two branches, so a single-line and a multi-line match of
the same edit produce the same bytes. `fs-edit`'s description states the rule, and
`FilePatchedReplacer` carries it in its XML doc.

**Tests.** `tests/LLMDesktopAssistant.Tests/Utils/FilePatchedReplacerTests.cs` gained four cases:
wrapping a body in a block, converting an expression-bodied member into a block, the same replacement
under a one-line and a two-line match, and a verbatim copy that must not be shifted twice. Three
existing cases were corrected, because they encoded the split behaviour this report is about:
`..._RelativeContinuation_CollapsesOntoTheMatchLevel` (now `..._IsAddedToTheMatchLevel` - the
"documented limit" was the bug), `..._FollowsTheColumnsOfTheContinuationLines` (now
`..._BarePatch_LandsOnTheMatchIndentation`), and `..._FirstLineWithoutIndentAndContinuationCopiedFromFile`,
whose match now carries the file's tabs. Full run: 1427 tests, 0 failed, 1 skipped
(`SkillParserTests.DescriptionWithColons_ParsesCorrectly`, a pre-existing YamlDotNet skip).

**Still open.** Point 6 of the proposed fix - `No changes were made` and `1 occurrence replaced` in
one answer - lives in `DescribeNoChange` in `FilesystemEditToolModule` and was not touched. The rule
also still rests on one inference: "the patch was copied out of the file" is read from the depth of
the match's second line, so a patch that happens to put its second line at the file's second line
without having been copied is treated as verbatim.
