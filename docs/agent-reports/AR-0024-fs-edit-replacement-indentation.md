# AR-0024 - `fs-edit` adds the matched indentation on top of the replacement's own, and only for single-line matches

- **Status:** open
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

## Why it costs

- **It produces silent breakage in review, not in build.** Over-indentation compiles; column 0 does not. So one variant ships quietly and the other fails loudly later - and which one you get depends on an invisible property of the `match`.
- **Every multi-line insertion needs a verification pass.** Either I open the file afterwards, or I trust a tool that just did the opposite of what the same tool did a minute ago. In this session that was five repair calls (`fs-apply_diff` per damaged region plus two regex passes over a whole file) and roughly eight wasted `fs-edit` attempts.
- **It pushes the work onto a worse tool.** The only reliable way to insert a block turned out to be `fs-apply_diff` with explicit line numbers, which means counting lines in a 400-line file - exactly the bookkeeping `fs-edit` exists to avoid.

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
```

## Witnesses

- **2026-10-25** - session adding synchronization analyzers to the fs tools - eleven `fs-edit` calls inserting a `SyncAnalyzer` line above a `Name = "..."` line across five modules, all over-indented by the match's indentation and all repaired by a regex pass; then a multi-line edit of `ToolCallViewModel.cs` whose continuation lines landed at column 0, repaired with `fs-apply_diff`. The user interrupted mid-session to point the behaviour out ("fs-edit works with RELATIVE indents"), which is the strongest evidence that the intended rule is not the one the tool follows.

## What I did instead

- For insertions: `fs-apply_diff` with `insertBeforeLine` + `insertText`, which writes the text exactly as given (and, to its credit, is the reason the C# was repairable at all).
- For bulk, mechanical changes: a Python script over the file with a regex, bypassing the tool entirely.
- For the indentation already damaged: a regex patch that rewrote every over-indented line back to four tabs.

That is three different mechanisms replacing one edit, and the only one that behaved predictably was not a tool at all.

## Proposed fix

1. **Pick one rule and state it in the description.** Cheapest and decides everything else: either "the replacement is inserted verbatim - indentation included" or "the replacement inherits the matched indentation and its own leading whitespace is stripped". Today's wording describes matching and leaves writing open, and the implementation does both at once depending on the match's shape.
2. **Make the two shapes agree.** Whatever the rule, a single-line and a multi-line `match` of the same block must produce the same bytes. This is the actual bug: the same logical edit behaves differently depending on whether I put a line break in `match`.
3. **Add a way to say "verbatim"** (`replaceVerbatim: true`) if the inherited-indentation rule is kept for the common single-line case. Then a multi-line block edit is expressible without guessing.
4. **Echo the replacement's indentation in the diff**, e.g. a whitespace-visible mode, so the first evidence is not "read the file and count tabs".

## Unverified

- The cause was not traced: the `fs-edit` implementation was not opened, and this session was working in the dASS repo, not in the tool's own code. What is observed is the input, the echo and the resulting bytes - not the branch that produced them.
- Whether `fs-apply_diff` inherits the same behaviour for multi-line `insertText` was not exercised deeply: every `insertText` in this session was written with absolute tabs and landed as written, which suggests it is verbatim, but a single counter-example would overturn that.
- The exact mechanism behind "single-line match re-indents, multi-line match does not" is guessed, not measured; a table of `match` shapes against their output would settle it in one run.
