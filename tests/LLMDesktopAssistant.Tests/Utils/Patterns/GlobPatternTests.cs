using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Utils.Patterns;

public class GlobPatternTests
{
	private static bool Match(string pattern, params string[] segments)
	{
		return GlobPattern.Parse(pattern, GlobOptions.NoPrefix).Matches(segments);
	}

	private static string[] Prefix(string pattern)
	{
		return [.. GlobPattern.Parse(pattern).Prefix];
	}

	[Fact]
	public void Star_StaysInsideOneSegment()
	{
		Assert.True(Match("*.cs", "a.cs"));
		Assert.False(Match("*.cs", "src", "a.cs"));
	}

	[Fact]
	public void DoubleStar_MatchesZeroSegments()
	{
		Assert.True(Match("**/*.cs", "a.cs"));
		Assert.True(Match("**/*.cs", "src", "a", "b.cs"));
		Assert.True(Match("a/**/b", "a", "b"));
		Assert.True(Match("a/**/b", "a", "x", "y", "b"));
		Assert.True(Match("a/**", "a"));
		Assert.True(Match("**", []));
	}

	[Fact]
	public void Star_RequiresASegment()
	{
		Assert.False(Match("*", []));
		Assert.True(Match("*", "a"));
	}

	[Fact]
	public void QuestionMark_MatchesOneCharacter()
	{
		Assert.True(Match("?", "a"));
		Assert.False(Match("?", "ab"));
		Assert.True(Match("FileUtil?.cs", "FileUtils.cs"));
	}

	[Fact]
	public void Classes_AndBraces()
	{
		Assert.True(Match("[abc].cs", "b.cs"));
		Assert.False(Match("[!abc].cs", "b.cs"));
		Assert.True(Match("[!abc].cs", "d.cs"));
		Assert.True(Match("*.lo[c]", "a.loc"));
		Assert.True(Match("*.{cs,loc}", "a.loc"));
		Assert.False(Match("*.{cs,loc}", "a.txt"));
	}

	[Fact]
	public void BracesStarAndClass_ComposeInsideOneSegment()
	{
		const string pattern = "{abc,def}a*8{bc,ef}[a-g]";

		Assert.True(Match(pattern, "abcax8bcd"));
		Assert.True(Match(pattern, "defa-8efg"));
		Assert.False(Match(pattern, "abca-8efh"));
		Assert.False(Match(pattern, "xyza-8efg"));

		Assert.Equal(4, GlobPattern.Parse(pattern).AlternativeTexts.Count);
	}

	[Fact]
	public void Normalization_CollapsesEmptySegments()
	{
		Assert.True(Match("a//b", "a", "b"));
		Assert.Equal([".", "a"], Prefix("./a/b"));
	}

	[Fact]
	public void TrailingSeparator_RequiresADirectory()
	{
		var pattern = GlobPattern.Parse("src/");

		Assert.True(pattern.RequiresDirectory);
		Assert.True(pattern.Matches([]));
	}

	[Fact]
	public void IgnoreCase_FollowsTheOption()
	{
		Assert.False(GlobPattern.Parse("*.CS").Matches(["a.cs"]));
		Assert.True(GlobPattern.Parse("*.CS", GlobOptions.IgnoreCase).Matches(["a.cs"]));
	}

	[Fact]
	public void Prefix_IsTheLeadingLiteralRun()
	{
		Assert.Equal(["C:", "MyFiles"], Prefix(@"C:\MyFiles\**\*.txt"));
		Assert.Equal(["src"], Prefix("src/**/*.cs"));
		Assert.Empty(Prefix("{src,lib}/**"));
		Assert.Equal(["src"], Prefix("src/{a,b}/**"));
		Assert.Equal(["..", "shared"], Prefix("../shared/**"));
		Assert.Empty(Prefix("[ab]/x"));
	}

	[Fact]
	public void Prefix_KeepsTheLastSegmentInTheRemainder()
	{
		var pattern = GlobPattern.Parse("src/lib/main.cs");

		Assert.Equal(["src", "lib"], Prefix("src/lib/main.cs"));
		Assert.True(pattern.Matches(["main.cs"]));
	}

	[Fact]
	public void Prefix_LeavesALoneLiteralNameInTheRemainder()
	{
		var pattern = GlobPattern.Parse("main.cs");

		Assert.Empty(pattern.Prefix);
		Assert.True(pattern.Matches(["main.cs"]));
	}

	[Fact]
	public void Prefix_TakesALoneLocationSegment()
	{
		Assert.Equal(["src"], Prefix("src/"));
		Assert.Equal([".."], Prefix(".."));
		Assert.Equal(["."], Prefix("."));
	}

	[Fact]
	public void Prefix_NormalizesABareDrive()
	{
		Assert.Equal(["C:/"], Prefix("C:"));
	}

	[Fact]
	public void Prefix_Disabled_KeepsOperatorsAsSegments()
	{
		var pattern = GlobPattern.Parse(@"C:\a\**", GlobOptions.NoPrefix);

		Assert.Empty(pattern.Prefix);
		Assert.True(pattern.Matches(["C:", "a", "b"]));
	}

	[Fact]
	public void Remainder_HasNoPrefixOfItsOwn()
	{
		var pattern = GlobPattern.Parse(@"C:\MyFiles\**\*.txt");

		Assert.Empty(pattern.Remainder.Prefix);
		Assert.True(pattern.Remainder.Matches(["a", "b.txt"]));
		Assert.Same(pattern.Remainder, pattern.Remainder.Remainder);
	}

	[Fact]
	public void NonWholeDoubleStar_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => GlobPattern.Parse("a**b"));

		Assert.Equal(GlobError.DoubleStarNotWholeSegment, exception.Error);
	}

	[Fact]
	public void DriveRelativePattern_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => GlobPattern.Parse(@"C:foo"));

		Assert.Equal(GlobError.DriveRelativePattern, exception.Error);
	}

	[Fact]
	public void MixedRootedness_IsRejected()
	{
		var exception = Assert.Throws<GlobException>(() => GlobPattern.Parse("{/a,b}"));

		Assert.Equal(GlobError.ConflictingRoot, exception.Error);
	}

	[Fact]
	public void ArgumentTail_IsRejectedForPaths()
	{
		var exception = Assert.Throws<GlobException>(
			() => GlobPattern.Parse("a", GlobOptions.ArgumentTail));

		Assert.Equal(GlobError.OptionNotSupported, exception.Error);
	}

	[Fact]
	public void ExplicitName_IsTrueOnlyForNamingSegments()
	{
		Assert.True(GlobPattern.Parse("**/obj/*.cs").IsExplicitName([], "obj"));
		Assert.False(GlobPattern.Parse("**/*.cs").IsExplicitName([], "obj"));
		Assert.True(GlobPattern.Parse(".*/config").IsExplicitName([], ".git"));
		Assert.True(GlobPattern.Parse("{.git,src}/**").IsExplicitName([], ".git"));
		Assert.False(GlobPattern.Parse("**").IsExplicitName([], ".git"));
	}

	[Fact]
	public void PrefixPossible_IsFalseForADeadSubtree()
	{
		var pattern = GlobPattern.Parse("src/lib/*.cs", GlobOptions.NoPrefix);

		Assert.True(pattern.IsPrefixPossible([]));
		Assert.True(pattern.IsPrefixPossible(["src"]));
		Assert.True(pattern.IsPrefixPossible(["src", "lib"]));
		Assert.False(pattern.IsPrefixPossible(["src", "x"]));
		Assert.False(pattern.IsPrefixPossible(["nope"]));
	}
}
