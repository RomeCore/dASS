using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tests.Utils;

public class UnifiedDiffTests
{
	[Fact]
	public void SameText_ReturnsEmpty()
	{
		var text = "line1\nline2\nline3";
		var result = UnifiedDiff.Compute(text, text, 3);
		Assert.False(result.HasGroups);
		Assert.Empty(result.Groups);
	}

	[Fact]
	public void EmptyText_ReturnsEmpty()
	{
		var result = UnifiedDiff.Compute("", "", 3);
		Assert.False(result.HasGroups);
	}

	[Fact]
	public void AppendLine_ShowsAddition()
	{
		var old = "line1\nline2";
		var @new = "line1\nline2\nline3";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		Assert.Single(result.Groups);

		var group = result.Groups[0];
		Assert.Contains(group.Lines, l => l.Kind == '+' && l.Content == "line3");
	}

	[Fact]
	public void DeleteFirstLine_ShowsRemoval()
	{
		var old = "line1\nline2\nline3";
		var @new = "line2\nline3";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(1, removed);
		Assert.Equal(0, added);
	}

	[Fact]
	public void ChangeSingleLine_ShowsRemoveAndAdd()
	{
		var old = "line1\nold_line\nline3";
		var @new = "line1\nnew_line\nline3";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(1, removed);
		Assert.Equal(1, added);
	}

	[Fact]
	public void ContextLines_Default3_ShowsCorrectContext()
	{
		// 10 context lines, change on line 11
		var old = Enumerable.Range(1, 15).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 15).Select(i => i == 11 ? $"LINE{11}_MODIFIED" : $"line{i}").ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		var result = UnifiedDiff.Compute(oldText, newText, 3);

		Assert.True(result.HasGroups);
		var group = result.Groups[0];

		// Expected: 3 context lines before (8,9,10) + 1 removal (11) + 1 addition (11) + 3 context lines after (12,13,14) = 8 lines
		// line15 should NOT be included (it's line 15, change on line 11, +3 = 14)
		Assert.Equal(8, group.Lines.Count);

		// First 3 lines - context BEFORE the change
		for (int i = 0; i < 3; i++)
			Assert.Equal(' ', group.Lines[i].Kind);

		// The changed line
		Assert.Equal('-', group.Lines[3].Kind);
		Assert.Equal('+', group.Lines[4].Kind);

		// Last 3 lines - context AFTER
		for (int i = 5; i < 8; i++)
			Assert.Equal(' ', group.Lines[i].Kind);
	}

	[Fact]
	public void MultipleChangesClose_FormsSingleHunk()
	{
		// Two changes 2 lines apart (less than contextLines=3)
		var old = Enumerable.Range(1, 10).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 10).Select(i => i switch
		{
			4 => "line4_MODIFIED",
			7 => "line7_MODIFIED",
			_ => $"line{i}"
		}).ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		var result = UnifiedDiff.Compute(oldText, newText, 3);

		// Should be ONE hunk since changes are less than 3 lines apart
		Assert.Single(result.Groups);
	}

	[Fact]
	public void MultipleChangesFar_CreatesMultipleHunks()
	{
		// Two changes 6 lines apart (greater than contextLines=3) - should produce two hunks
		// Edge case: context between them
		var old = Enumerable.Range(1, 20).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 20).Select(i => i switch
		{
			4 => "line4_MODIFIED",
			14 => "line14_MODIFIED",
			_ => $"line{i}"
		}).ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		var result = UnifiedDiff.Compute(oldText, newText, 3);

		// Should be TWO hunks
		Assert.Equal(2, result.Groups.Count);

		// First hunk: change on line 4 (0-based: 3)
		var hunk1 = result.Groups[0];
		Assert.Equal(1, hunk1.OldStart); // 4-3 = 1 (1-based)
		// Second hunk: change on line 14 (0-based: 13)
		var hunk2 = result.Groups[1];
		Assert.Equal(11, hunk2.OldStart); // 14-3 = 11 (1-based)
	}

	[Fact]
	public void TrailingContext_NotLostBetweenHunks()
	{
		// Bug: trailing context of the first hunk could be lost
		// Change on line 5, then 10 context lines, then change on line 16
		// With contextLines=3:
		//   Hunk 1: lines 2-8 (5-3=2, 5+3=8)
		//   Hunk 2: lines 13-19 (16-3=13, min(16+3, last))
		//   Lines 9-12 should NOT be duplicated between hunks
		var old = Enumerable.Range(1, 22).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 22).Select(i => i switch
		{
			5 => "line5_MODIFIED",
			16 => "line16_MODIFIED",
			_ => $"line{i}"
		}).ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		var result = UnifiedDiff.Compute(oldText, newText, 3);

		Assert.Equal(2, result.Groups.Count);

		// Verify no line duplication between hunks
		var hunk1Lines = result.Groups[0].Lines.Select(l => l.Content).ToList();
		var hunk2Lines = result.Groups[1].Lines.Select(l => l.Content).ToList();

		// The last context line of hunk 1 (line8) should NOT be
		// the first context line of hunk 2
		if (hunk1Lines.Count > 0 && hunk2Lines.Count > 0)
		{
			var lastOfHunk1 = hunk1Lines.Last();
			var firstOfHunk2 = hunk2Lines.First();
			Assert.NotEqual(lastOfHunk1, firstOfHunk2);
		}
	}

	[Fact]
	public void HunkGroup_Numbers_CorrectForAppend()
	{
		var old = "line1\nline2\nline3";
		var @new = "line1\nline2\nline3\nline4\nline5";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		var group = result.Groups[0];

		// Append at the end: old starts from the last context lines
		Assert.Equal(1, group.OldStart);
		Assert.Equal(3, group.OldCount); // 3 context lines
		Assert.Equal(1, group.NewStart);
		Assert.Equal(5, group.NewCount); // 3 context + 2 new lines
	}

	[Fact]
	public void HunkGroup_Numbers_CorrectForPrepend()
	{
		var old = "line3\nline4\nline5";
		var @new = "line1\nline2\nline3\nline4\nline5";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		var group = result.Groups[0];

		// Prepend at the beginning: old starts at 1
		Assert.Equal(1, group.OldStart);
		Assert.Equal(3, group.OldCount); // only 3 context lines (line3,line4,line5)
		Assert.Equal(1, group.NewStart);
		Assert.Equal(5, group.NewCount); // 3 context + 2 new lines
	}

	[Fact]
	public void EmptyOldText_WithNewContent_ShowsAllAdded()
	{
		var result = UnifiedDiff.Compute("", "line1\nline2\nline3", 3);

		Assert.True(result.HasGroups);
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(0, removed);
		Assert.Equal(3, added);
	}

	[Fact]
	public void EmptyNewText_WithOldContent_ShowsAllRemoved()
	{
		var result = UnifiedDiff.Compute("line1\nline2\nline3", "", 3);

		Assert.True(result.HasGroups);
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(3, removed);
		Assert.Equal(0, added);
	}

	[Fact]
	public void SingleCharacterChange_Works()
	{
		var result = UnifiedDiff.Compute("abc", "abd", 3);

		Assert.True(result.HasGroups);
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(1, removed);
		Assert.Equal(1, added);
	}

	[Fact]
	public void MultipleChangesInSameArea_ShowsCorrectly()
	{
		var old = "a\nb\nc\nd\ne\nf\ng";
		var @new = "a\nx\ny\nd\ne\nz\ng";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		// b->x, c->y, f->z
		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(3, removed);
		Assert.Equal(3, added);
	}

	[Fact]
	public void LargeContext_RespectsContextLinesParameter()
	{
		var old = Enumerable.Range(1, 50).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 50).Select(i => i == 25 ? "line25_MODIFIED" : $"line{i}").ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		// contextLines = 0 - no context at all
		var result0 = UnifiedDiff.Compute(oldText, newText, 0);
		Assert.True(result0.HasGroups);
		var group0 = result0.Groups[0];
		Assert.Equal(0, group0.Lines.Count(l => l.Kind == ' ')); // change only, no context

		// contextLines = 10
		var result10 = UnifiedDiff.Compute(oldText, newText, 10);
		Assert.True(result10.HasGroups);
		var group10 = result10.Groups[0];
		Assert.Equal(22, group10.Lines.Count); // 10 context before + 1 removal + 1 addition + 10 after
	}

	[Fact]
	public void GetChangeCounts_ReturnsCorrectCounts()
	{
		var old = "a\nb\nc\nd\ne";
		var @new = "a\nx\nc\ny\ne\nf";
		var result = UnifiedDiff.Compute(old, @new, 3);

		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(2, removed); // b, d
		Assert.Equal(3, added);   // x, y, f
	}

	[Fact]
	public void ToString_ProducesValidUnifiedDiffFormat()
	{
		var old = "hello\nworld\nfoo\nbar\nbaz";
		var @new = "hello\nworld\nfoo\nMODIFIED\nbaz\nnewline";
		var result = UnifiedDiff.Compute(old, @new, 2);

		var diffString = result.ToString();
		Assert.NotEmpty(diffString);

		// Should contain hunk header
		Assert.StartsWith("@@", diffString);

		// Should contain removed line
		Assert.Contains("-bar", diffString);

		// Should contain added line
		Assert.Contains("+MODIFIED", diffString);

		// Should contain added line at the end
		Assert.Contains("+newline", diffString);
	}

	[Fact]
	public void AdjacentChanges_WithLeadingAndTrailingContext_NotCorrupted()
	{
		// Scenario: changes at the beginning and end, lots of context in the middle
		// Bug: context lines could be duplicated or lost
		var old = Enumerable.Range(1, 30).Select(i => $"line{i}").ToArray();
		var @new = Enumerable.Range(1, 30).Select(i => i switch
		{
			2 => "line2_MOD",
			28 => "line28_MOD",
			_ => $"line{i}"
		}).ToArray();
		var oldText = string.Join("\n", old);
		var newText = string.Join("\n", @new);

		var result = UnifiedDiff.Compute(oldText, newText, 3);

		// Should be 2 hunks
		Assert.Equal(2, result.Groups.Count);

		// Verify that all lines from the diff can be matched to the originals
		// No line should be lost or duplicated
		var allDiffLines = result.Groups
			.SelectMany(g => g.Lines)
			.ToList();

		// Context lines (' ') should match the old version
		foreach (var line in allDiffLines.Where(l => l.Kind == ' '))
		{
			Assert.Contains(line.Content, old);
		}

		// Removed lines ('-') should be in old
		foreach (var line in allDiffLines.Where(l => l.Kind == '-'))
		{
			Assert.Contains(line.Content, old);
		}

		// Added lines ('+') should be in new
		foreach (var line in allDiffLines.Where(l => l.Kind == '+'))
		{
			Assert.Contains(line.Content, @new);
		}
	}

	[Fact]
	public void ContextLines_ChangeAtFirstLine_OnlyTrailingContext()
	{
		// Change at the very first line - no leading context possible
		var old = "line1\nline2\nline3\nline4\nline5";
		var @new = "MODIFIED\nline2\nline3\nline4\nline5";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		Assert.Single(result.Groups);
		var group = result.Groups[0];

		// Hunk: 0 context before + 1 removal + 1 addition + 3 context after = 5 lines
		Assert.Equal(5, group.Lines.Count);

		// First two lines should be the change (remove old + add new)
		Assert.Equal('-', group.Lines[0].Kind);
		Assert.Equal("line1", group.Lines[0].Content);
		Assert.Equal('+', group.Lines[1].Kind);
		Assert.Equal("MODIFIED", group.Lines[1].Content);

		// Remaining three should be trailing context: line2, line3, line4
		for (int i = 2; i < 5; i++)
		{
			Assert.Equal(' ', group.Lines[i].Kind);
			Assert.Equal($"line{i}", group.Lines[i].Content); // line2, line3, line4
		}

		// line5 should NOT be present (it's the 4th trailing line, beyond contextLines=3)
		Assert.DoesNotContain(group.Lines, l => l.Content == "line5");
	}

	[Fact]
	public void ContextLines_ChangeAtLastLine_OnlyLeadingContext()
	{
		// Change at the very last line - no trailing context possible
		var old = "line1\nline2\nline3\nline4\nline5";
		var @new = "line1\nline2\nline3\nline4\nMODIFIED";
		var result = UnifiedDiff.Compute(old, @new, 3);

		Assert.True(result.HasGroups);
		Assert.Single(result.Groups);
		var group = result.Groups[0];

		// Hunk: 3 context before (line2, line3, line4) + 1 removal + 1 addition = 5 lines
		Assert.Equal(5, group.Lines.Count);

		// First three should be leading context: line2, line3, line4
		for (int i = 0; i < 3; i++)
		{
			Assert.Equal(' ', group.Lines[i].Kind);
			Assert.Equal($"line{i + 2}", group.Lines[i].Content);
		}

		// line1 should NOT be present (it's the 4th leading line, beyond contextLines=3)
		Assert.DoesNotContain(group.Lines, l => l.Content == "line1");

		// Last two: removal then addition
		Assert.Equal('-', group.Lines[3].Kind);
		Assert.Equal("line5", group.Lines[3].Content);
		Assert.Equal('+', group.Lines[4].Kind);
		Assert.Equal("MODIFIED", group.Lines[4].Content);
	}

	[Fact]
	public void ContextLines_LargerThanFile_IncludesAllLines()
	{
		// contextLines = 100, file has only 5 lines - all lines should be included
		var old = "line1\nline2\nCHANGE_ME\nline4\nline5";
		var @new = "line1\nline2\nCHANGED\nline4\nline5";
		var result = UnifiedDiff.Compute(old, @new, 100);

		Assert.True(result.HasGroups);
		Assert.Single(result.Groups);
		var group = result.Groups[0];

		// All 5 old lines should participate: 2 context before + 1 removal + 2 context after = 5
		Assert.Equal(5, group.OldCount);
		// All 5 new lines: 2 context before + 1 addition + 2 context after = 5
		Assert.Equal(5, group.NewCount);

		// Verify the exact sequence
		Assert.Equal(' ', group.Lines[0].Kind); // line1
		Assert.Equal(' ', group.Lines[1].Kind); // line2
		Assert.Equal('-', group.Lines[2].Kind); // CHANGE_ME
		Assert.Equal('+', group.Lines[3].Kind); // CHANGED
		Assert.Equal(' ', group.Lines[4].Kind); // line4
		Assert.Equal(' ', group.Lines[5].Kind); // line5

		Assert.Equal(6, group.Lines.Count);
	}

	[Fact]
	public void ContextLines_ReplacementWithDifferentCounts_CorrectContext()
	{
		// Replace 1 line with 3 lines - verify context counts stay correct
		var old = "a\nb\nOLD\nc\nd";
		var @new = "a\nb\nNEW1\nNEW2\nNEW3\nc\nd";
		var result = UnifiedDiff.Compute(old, @new, 2);

		Assert.True(result.HasGroups);
		Assert.Single(result.Groups);
		var group = result.Groups[0];

		// Old: 2 context before (a,b) + 1 removed (OLD) + 2 context after (c,d) = 5
		Assert.Equal(5, group.OldCount);
		// New: 2 context before (a,b) + 3 added (NEW1,NEW2,NEW3) + 2 context after (c,d) = 7
		Assert.Equal(7, group.NewCount);

		// Verify sequence: a, b, -OLD, +NEW1, +NEW2, +NEW3, c, d
		Assert.Equal(8, group.Lines.Count);
		Assert.Equal(' ', group.Lines[0].Kind); // a
		Assert.Equal(' ', group.Lines[1].Kind); // b
		Assert.Equal('-', group.Lines[2].Kind); // OLD
		Assert.Equal('+', group.Lines[3].Kind); // NEW1
		Assert.Equal('+', group.Lines[4].Kind); // NEW2
		Assert.Equal('+', group.Lines[5].Kind); // NEW3
		Assert.Equal(' ', group.Lines[6].Kind); // c
		Assert.Equal(' ', group.Lines[7].Kind); // d

		var (removed, added) = result.GetChangeCounts();
		Assert.Equal(1, removed);
		Assert.Equal(3, added);
	}
}
