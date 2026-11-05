using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Utils.Patterns;

public class GlobPatternParserTests
{
	private static string Parse(string pattern)
	{
		var alternatives = GlobPatternParser.ParseAlternatives(pattern);
		return string.Join("|", alternatives.Select(GlobExpander.Render));
	}

	[Fact]
	public void Parse_PlainLiterals_RenderAsWritten()
	{
		Assert.Equal("file.txt", Parse("file.txt"));
	}

	[Fact]
	public void Parse_Wildcards_RenderAsWritten()
	{
		Assert.Equal("FileUtil?.cs", Parse("FileUtil?.cs"));
		Assert.Equal("**/x", Parse("**/x"));
		Assert.Equal("a**b", Parse("a**b"));
	}

	[Fact]
	public void Parse_BraceWithoutComma_IsLiteral()
	{
		Assert.Equal("{a}", Parse("{a}"));
	}

	[Fact]
	public void Parse_UnterminatedBrace_IsLiteral()
	{
		Assert.Equal("{a,b", Parse("{a,b"));
	}

	[Fact]
	public void Parse_Brace_ExpandsIntoAlternatives()
	{
		Assert.Equal("a|b", Parse("{a,b}"));
		Assert.Equal("ab|ac|d", Parse("{a{b,c},d}"));
	}

	[Fact]
	public void Parse_UnterminatedClass_IsLiteral()
	{
		Assert.Equal("[a", Parse("[a"));
	}

	[Fact]
	public void Parse_ClassOfClosingBracket_IsOneUnit()
	{
		var units = GlobPatternParser.Parse("[]]");

		var cls = Assert.IsType<GlobClass>(Assert.Single(units));
		Assert.Equal("]", cls.Body);
	}

	[Fact]
	public void Parse_ClassWithNegationAndRange_KeepsRawBody()
	{
		var cls = Assert.IsType<GlobClass>(Assert.Single(GlobPatternParser.Parse("[!a-c]")));

		Assert.Equal("!a-c", cls.Body);
	}

	[Fact]
	public void Parse_ColonStar_IsTwoOrdinaryUnits()
	{
		var units = GlobPatternParser.Parse("C:*");

		Assert.Equal(3, units.Count);
		Assert.Equal(':', Assert.IsType<GlobLiteral>(units[1]).Value);
		Assert.IsType<GlobAnyChars>(units[2]);
	}

	[Fact]
	public void Parse_StrayClosers_AreLiterals()
	{
		Assert.Equal("a]b}", Parse("a]b}"));
	}

	[Fact]
	public void Parse_Positions_PointIntoTheSourcePattern()
	{
		var units = GlobPatternParser.Parse("ab[c]");

		Assert.Equal(0, units[0].Position);
		Assert.Equal(1, units[1].Position);
		Assert.Equal(2, units[2].Position);
	}
}
