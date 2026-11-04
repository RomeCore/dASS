using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tests.Utils;

/// <summary>
/// Tests for <see cref="FilePatchedReplacer"/>: the rule by which a patch's replacement is written
/// into a file. Every case here is a shape that produced a wrong file in a real session, so the
/// expected bytes are derived from the rule by hand, never taken from the implementation's output.
/// </summary>
public class FilePatchedReplacerTests
{
	[Fact]
	public void Replace_FirstLineWithoutIndentAndContinuationCopiedFromFile_LandsAtFileIndentation()
	{
		// AR-0024, call 1: the patch is copied out of the file, so the match and the continuation
		// line both carry the file's own tabs. That must land once, not doubled.
		string content = "\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n";
		string match = "\t\t\t\tExecutor = Something,";
		string replace = "SyncAnalyzer = A,\n\t\t\t\tExecutor = Something,";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal(
			"\t\t\t\tSyncAnalyzer = A,\n\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n",
			application.Content);
	}

	[Fact]
	public void Replace_FullyDedentedPatch_LandsAtFileIndentation()
	{
		string content = "\t\tint a = 1;\n\t\treturn a;\n";
		string match = "int a = 1;\nreturn a;";
		string replace = "int a = 2;\nreturn a + 1;";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tint a = 2;\n\t\treturn a + 1;\n", application.Content);
	}

	[Fact]
	public void Replace_GrowingBlock_KeepsTheNestingThatWasWritten()
	{
		// One shift for the whole block: the shape of the replacement is preserved, so the block that
		// was written nested lands nested, at the level the match started at.
		string content = "\t\tif (a)\n";
		string match = "if (a)";
		string replace = "if (a)\n{\n\treturn;\n}";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tif (a)\n\t\t{\n\t\t\treturn;\n\t\t}\n", application.Content);
	}

	[Fact]
	public void Replace_SpaceIndentedFile_KeepsSpaces()
	{
		string content = """
			    if (a)
			        return;

			""".ReplaceLineEndings("\n");
			
		string match = """
			if (a)
					return;
			""".ReplaceLineEndings("\n");
		string replace = """
			if (a)
				{
					return;
				}
			""".ReplaceLineEndings("\n");

		// The patch puts the brace one level deeper than the match, and that shape is what lands.
		var application = FilePatchedReplacer.Replace(content, match, replace);

		string expected = """
			    if (a)
			        {
			            return;
			        }

			""".ReplaceLineEndings("\n");
		Assert.Equal(expected, application.Content);
	}

	[Fact]
	public void Replace_TrailingCommentOnLastMatchedLine_IsPreserved()
	{
		string content = "\t\tif (a)\n\t\t{\n\t\t} // tail\n";
		string match = "if (a)\n{\n}";
		string replace = "while (a)\n{\n\tstep();\n}";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal("\t\twhile (a)\n\t\t{\n\t\t\tstep();\n\t\t} // tail\n", application.Content);
	}

	[Fact]
	public void Delete_WholeLineBlock_LeavesNoIndentationResidue()
	{
		// AR-0023: the match covers whole lines, so the lines go with their line breaks instead of
		// leaving a line of tabs where the separator used to be.
		string content = "line1;\n\n\t\tmember;\n\t\t{\n\t\t\tbody();\n\t\t}\n\nline2;\n";
		string match = "member;\n{\n\tbody();\n}";

		var application = FilePatchedReplacer.Replace(content, match, "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("line1;\n\n\nline2;\n", application.Content);
	}

	[Fact]
	public void Delete_SingleWholeLine_LeavesAnEmptyLineNotTabs()
	{
		// A single-line match deletes content, not structure: the line stays, but its indentation
		// must not survive as whitespace-only residue.
		string content = "a;\n\t\tTabs = 1;\nb;\n";

		var application = FilePatchedReplacer.Replace(content, "Tabs = 1;", "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("a;\n\nb;\n", application.Content);
	}

	[Fact]
	public void Delete_FragmentInsideLine_KeepsTheTextAroundIt()
	{
		string content = "\t\tint a = 1; // x\n";

		var application = FilePatchedReplacer.Replace(content, "a = 1", "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tint ; // x\n", application.Content);
	}

	[Fact]
	public void Replace_CountsEveryOccurrence()
	{
		string content = "value: 1\nother\nvalue: 1\n";

		var application = FilePatchedReplacer.Replace(content, "value: 1", "value: 2");

		Assert.Equal(2, application.Occurrences);
		Assert.Equal("value: 2\nother\nvalue: 2\n", application.Content);
	}

	[Fact]
	public void Replace_NoMatch_ReturnsNoContent()
	{
		var application = FilePatchedReplacer.Replace("abc\n", "zzz", "yyy");

		Assert.Equal(0, application.Occurrences);
		Assert.Null(application.Content);
	}

	[Fact]
	public void Replace_BlankLineInMatch_DoesNotSwallowARealLine()
	{
		// A blank line in the patch means a blank line in the file; matching any line there would
		// make the block silently eat whatever happens to sit between the two anchors.
		var application = FilePatchedReplacer.Replace("a\nX\nb\n", "a\n\nb", "c");

		Assert.Equal(0, application.Occurrences);
		Assert.Null(application.Content);
	}

	[Fact]
	public void Replace_BlankLineInMatch_MatchesABlankFileLine()
	{
		var application = FilePatchedReplacer.Replace("a\n\nb\n", "a\n\nb", "c");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("c\n", application.Content);
	}

	[Fact]
	public void Replace_SingleLineMatch_BarePatch_LandsOnTheMatchIndentation()
	{
		// A match that starts at column zero says the patch was written from scratch, so the whole
		// block moves by the indentation of the line the match was found on.
		string content = "\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n";
		string replace = "Sync = A,\nExecutor = Something,";

		var application = FilePatchedReplacer.Replace(content, "Executor = Something,", replace);

		Assert.Equal("\t\t\t\tSync = A,\n\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n", application.Content);
	}

	[Fact]
	public void Replace_SingleLineMatch_RelativeContinuation_IsAddedToTheMatchLevel()
	{
		// The match sits at column zero, so the patch is relative: the continuation's one tab is
		// added to the match's level, and "one level deeper" finally means one level deeper.
		string content = "\tif x:\n\t\treturn;\n";

		var application = FilePatchedReplacer.Replace(content, "if x:", "if x:\n\tdo();");

		Assert.Equal("\tif x:\n\t\tdo();\n\t\treturn;\n", application.Content);
	}

	[Fact]
	public void Replace_SingleLineMatch_FlatBlock_LandsOnTheMatchLevel()
	{
		// A block written flat has a line at column zero, and that line anchors the whole block on
		// the match, so nesting written above it survives.
		string content = "\tif x:\n\t\treturn;\n";

		var application = FilePatchedReplacer.Replace(content, "if x:", "if x:\n{\n\tdo();\n}");

		Assert.Equal("\tif x:\n\t{\n\t\tdo();\n\t}\n\t\treturn;\n", application.Content);
	}

	[Fact]
	public void Replace_InLineMatchAtLineStart_KeepsTheReplacementIndentation()
	{
		// Nothing in the file supplies the indentation here, so the patch has to.
		var application = FilePatchedReplacer.Replace("Foo\n", "Foo", "\tBar");

		Assert.Equal("\tBar\n", application.Content);
	}

	[Fact]
	public void Replace_MultiLineMatch_WrappingInABlock_KeepsTheWrittenNesting()
	{
		// AR-0024, reopened 2026-11-04: the block is written one level deeper than the match and
		// must land one level deeper. The second matched line's depth is not the block's anchor.
		string content = """
					if (x)
						Do();

		""".ReplaceLineEndings("\n");
		string match = """
		if (x)
			Do();
		""".ReplaceLineEndings("\n");
		string replace = """
		if (x)
		{
			Do();
		}
		""".ReplaceLineEndings("\n");
		string expected = """
					if (x)
					{
						Do();
					}

		""".ReplaceLineEndings("\n");

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal(expected, application.Content);
	}

	[Fact]
	public void Replace_MultiLineMatch_ConvertingAMemberToABlock_KeepsTheWrittenNesting()
	{
		// AR-0024, the 2026-11-04 session: the block is written relative to the match, so it
		// must land there - not one level deeper, as aligning on the second matched line made it.
		string content = """
				public bool CanCancel =>
					!Terminal();

		""".ReplaceLineEndings("\n");
		string match = """
		public bool CanCancel =>
			!Terminal();
		""".ReplaceLineEndings("\n");
		string replace = """
		public bool CanCancel
		{
			get => !Terminal();
		}
		""".ReplaceLineEndings("\n");
		string expected = """
				public bool CanCancel
				{
					get => !Terminal();
				}

		""".ReplaceLineEndings("\n");

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal(expected, application.Content);
	}

	[Fact]
	public void Replace_BothMatchShapes_ApplyTheSameShiftToTheSameReplacement()
	{
		// The shape of the match may decide nothing but whether the second line survives. The block
		// it writes must land in the same place either way (AR-0024, proposed fix 2).
		string content = """
					if (x)
						Do();

		""".ReplaceLineEndings("\n");
		string replace = """
		if (x)
		{
			Do();
		}
		""".ReplaceLineEndings("\n");
		string expected = """
					if (x)
					{
						Do();
					}

		""".ReplaceLineEndings("\n");
		string twoLineMatch = """
		if (x)
			Do();
		""".ReplaceLineEndings("\n");
		string oneLineMatch = "if (x)";
		string oneLineExpected = """
					if (x)
					{
						Do();
					}
						Do();

		""".ReplaceLineEndings("\n");

		var withTwoMatchLines = FilePatchedReplacer.Replace(content, twoLineMatch, replace);
		var withOneMatchLine = FilePatchedReplacer.Replace(content, oneLineMatch, replace);

		Assert.Equal(expected, withTwoMatchLines.Content);
		Assert.Equal(oneLineExpected, withOneMatchLine.Content);
	}

	[Fact]
	public void Replace_MultiLineMatch_VerbatimCopyOfTheFile_IsNotShiftedAgain()
	{
		// Both the match and the replacement are copied out of the file, so the patch already sits in
		// the file's own columns and the shift has to be zero.
		string content = """
				if (a)
				{
					Call();
				}

		""".ReplaceLineEndings("\n");
		string match = """
				if (a)
				{
					Call();
				}
		""".ReplaceLineEndings("\n");
		string replace = """
				if (b)
				{
					Call();
				}
		""".ReplaceLineEndings("\n");
		string expected = """
				if (b)
				{
					Call();
				}

		""".ReplaceLineEndings("\n");

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal(expected, application.Content);
	}

	[Fact]
	public void Replace_IgnoreCase_KeepsTheReplacementAsWritten()
	{
		var application = FilePatchedReplacer.Replace("\t\tFOO\n", "foo", "bar", ignoreCase: true);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tbar\n", application.Content);
	}
}
