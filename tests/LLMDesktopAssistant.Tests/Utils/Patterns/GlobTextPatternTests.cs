using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Utils.Patterns;

public class GlobTextPatternTests
{
	private static GlobTextPattern Text(string pattern, GlobOptions options = GlobOptions.None)
	{
		return GlobTextPattern.Parse(pattern, options);
	}

	private static GlobTextPattern Command(string pattern)
	{
		return GlobTextPattern.Parse(pattern, GlobOptions.ArgumentTail);
	}

	[Fact]
	public void CommandTail_MatchesBareCommand()
	{
		Assert.True(Command("git:*").Matches("git"));
	}

	[Fact]
	public void CommandTail_MatchesArguments()
	{
		Assert.True(Command("git:*").Matches("git status"));
		Assert.True(Command("git:*").Matches("git "));
	}

	[Fact]
	public void CommandTail_MatchesTabSeparatedArguments()
	{
		Assert.True(Command("git:*").Matches("git\tstatus"));
	}

	[Fact]
	public void CommandTail_DoesNotMatchNewline()
	{
		Assert.False(Command("git:*").Matches("git\nstatus"));
	}

	[Fact]
	public void CommandTail_DoesNotMatchGluedText()
	{
		Assert.False(Command("git:*").Matches("gitty"));
	}

	[Fact]
	public void ArgumentTail_IsAFlagNotGrammar()
	{
		Assert.False(Text("git:*").Matches("git"));
		Assert.True(Text("git:*", GlobOptions.ArgumentTail).Matches("git"));
	}

	[Fact]
	public void Star_CrossesSeparatorsAndMatchesEmpty()
	{
		Assert.True(Text("*.cs").Matches("a/b.cs"));
		Assert.True(Text("*").Matches(string.Empty));
	}

	[Fact]
	public void Class_GivesLiteralStarAndQuestionMark()
	{
		Assert.True(Text("[*]").Matches("*"));
		Assert.True(Text("[?]").Matches("?"));
		Assert.False(Text("[*]").Matches("x"));
	}

	[Fact]
	public void Class_NegationAndRange()
	{
		Assert.True(Text("[a-c]x").Matches("bx"));
		Assert.False(Text("[a-c]x").Matches("dx"));
		Assert.True(Text("[!a-c]x").Matches("dx"));
		Assert.False(Text("[!a-c]x").Matches("bx"));
	}

	[Fact]
	public void Dot_IsAnOrdinaryCharacter()
	{
		Assert.True(Text("tool.*.fs-glob").Matches("tool.name.fs-glob"));
	}

	[Fact]
	public void Brace_ExpandsIntoAlternatives()
	{
		Assert.True(Text("*.{cs,loc}").Matches("a.loc"));
		Assert.False(Text("*.{cs,loc}").Matches("a.txt"));
	}

	[Fact]
	public void IgnoreCase_FollowsTheOption()
	{
		Assert.False(Text("*.CS").Matches("a.cs"));
		Assert.True(Text("*.CS", GlobOptions.IgnoreCase).Matches("a.cs"));
	}

	[Fact]
	public void DoubleStar_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => Text("**/x"));

		Assert.Equal(GlobError.DoubleStarUnsupported, exception.Error);
	}

	[Fact]
	public void ArgumentTailOffTheEnd_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => Command("a:*b"));

		Assert.Equal(GlobError.ArgumentTailNotTerminal, exception.Error);
	}

	[Fact]
	public void ReversedRange_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => Text("[z-a]"));

		Assert.Equal(GlobError.InvalidClassRange, exception.Error);
	}

	[Fact]
	public void PathOnlyOptions_AreRejected()
	{
		var exception = Assert.Throws<GlobException>(() => Text("a", GlobOptions.NoPrefix));

		Assert.Equal(GlobError.OptionNotSupported, exception.Error);
	}

	[Fact]
	public void EmptyPattern_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => Text("  "));

		Assert.Equal(GlobError.Empty, exception.Error);
	}
}

public class GlobTextSetTests
{
	[Fact]
	public void Names_MatchAnyOfTheSet()
	{
		var set = GlobTextSet.ParseNames([".*", "node_modules", "obj"]);

		Assert.True(set.Matches(".git"));
		Assert.True(set.Matches("node_modules"));
		Assert.True(set.Matches("obj"));
		Assert.False(set.Matches("src"));
	}

	[Fact]
	public void Names_RejectSeparators()
	{
		var exception = Assert.Throws<GlobException>(() => GlobTextSet.ParseNames(["**/bin"]));

		Assert.Equal(GlobError.SegmentPatternExpected, exception.Error);
	}

	[Fact]
	public void Empty_MatchesNothing()
	{
		Assert.False(GlobTextSet.Empty.Matches("anything"));
	}
}
