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
		// AR-0024, call 1: a verbatim-trained model writes the first replacement line bare and copies
		// the continuation line with the file's own tabs. That must land once, not doubled.
		const string content = "\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n";
		const string match = "Executor = Something,";
		const string replace = "SyncAnalyzer = A,\n\t\t\t\tExecutor = Something,";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal(
			"\t\t\t\tSyncAnalyzer = A,\n\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n",
			application.Content);
	}

	[Fact]
	public void Replace_FullyDedentedPatch_LandsAtFileIndentation()
	{
		const string content = "\t\tint a = 1;\n\t\treturn a;\n";
		const string match = "int a = 1;\nreturn a;";
		const string replace = "int a = 2;\nreturn a + 1;";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tint a = 2;\n\t\treturn a + 1;\n", application.Content);
	}

	[Fact]
	public void Replace_GrowingBlock_KeepsTheNestingThatWasWritten()
	{
		// One shift for the whole block: the shape of the replacement is preserved, so the block that
		// was written nested lands nested, at the level the match started at.
		const string content = "\t\tif (a)\n";
		const string match = "if (a)";
		const string replace = "if (a)\n{\n\treturn;\n}";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tif (a)\n\t\t{\n\t\t\treturn;\n\t\t}\n", application.Content);
	}

	[Fact]
	public void Replace_SpaceIndentedFile_KeepsSpaces()
	{
		const string content = "    if (a)\n        return;\n";
		const string match = "if (a)\nreturn;";
		const string replace = "if (a)\n\t{\n\t\treturn;\n\t}";

		// The patch puts the brace one level deeper than the match, and that shape is what lands.
		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal("    if (a)\n        {\n            return;\n        }\n", application.Content);
	}

	[Fact]
	public void Replace_TrailingCommentOnLastMatchedLine_IsPreserved()
	{
		const string content = "\t\tif (a)\n\t\t{\n\t\t} // tail\n";
		const string match = "if (a)\n{\n}";
		const string replace = "while (a)\n{\n\tstep();\n}";

		var application = FilePatchedReplacer.Replace(content, match, replace);

		Assert.Equal("\t\twhile (a)\n\t\t{\n\t\t\tstep();\n\t\t} // tail\n", application.Content);
	}

	[Fact]
	public void Delete_WholeLineBlock_LeavesNoIndentationResidue()
	{
		// AR-0023: the match covers whole lines, so the lines go with their line breaks instead of
		// leaving a line of tabs where the separator used to be.
		const string content = "line1;\n\n\t\tmember;\n\t\t{\n\t\t\tbody();\n\t\t}\n\nline2;\n";
		const string match = "member;\n{\n\tbody();\n}";

		var application = FilePatchedReplacer.Replace(content, match, "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("line1;\n\n\nline2;\n", application.Content);
	}

	[Fact]
	public void Delete_SingleWholeLine_LeavesAnEmptyLineNotTabs()
	{
		// A single-line match deletes content, not structure: the line stays, but its indentation
		// must not survive as whitespace-only residue.
		const string content = "a;\n\t\tTabs = 1;\nb;\n";

		var application = FilePatchedReplacer.Replace(content, "Tabs = 1;", "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("a;\n\nb;\n", application.Content);
	}

	[Fact]
	public void Delete_FragmentInsideLine_KeepsTheTextAroundIt()
	{
		const string content = "\t\tint a = 1; // x\n";

		var application = FilePatchedReplacer.Replace(content, "a = 1", "");

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tint ; // x\n", application.Content);
	}

	[Fact]
	public void Replace_CountsEveryOccurrence()
	{
		const string content = "value: 1\nother\nvalue: 1\n";

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
	public void Replace_SingleLineMatch_FollowsTheColumnsOfTheContinuationLines()
	{
		const string content = "\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n";
		const string replace = "Sync = A,\n\t\t\t\tExecutor = Something,";

		var application = FilePatchedReplacer.Replace(content, "Executor = Something,", replace);

		Assert.Equal("\t\t\t\tSync = A,\n\t\t\t\tExecutor = Something,\n\t\t\t\tOther = 1;\n", application.Content);
	}

	[Fact]
	public void Replace_SingleLineMatch_RelativeContinuation_CollapsesOntoTheMatchLevel()
	{
		// Documented limit: with a one-line match "one level deeper" and "the file's own columns"
		// are the same bytes, and the tool reads them as columns. Spell the level out instead.
		const string content = "\tif x:\n\t\treturn;\n";

		var application = FilePatchedReplacer.Replace(content, "if x:", "if x:\n\tdo();");

		Assert.Equal("\tif x:\n\tdo();\n\t\treturn;\n", application.Content);
	}

	[Fact]
	public void Replace_SingleLineMatch_FlatBlock_LandsOnTheMatchLevel()
	{
		// A block written flat has a line at column zero, and that line anchors the whole block on
		// the match, so nesting written above it survives.
		const string content = "\tif x:\n\t\treturn;\n";

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
	public void Replace_IgnoreCase_KeepsTheReplacementAsWritten()
	{
		var application = FilePatchedReplacer.Replace("\t\tFOO\n", "foo", "bar", ignoreCase: true);

		Assert.Equal(1, application.Occurrences);
		Assert.Equal("\t\tbar\n", application.Content);
	}
}
