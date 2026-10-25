using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Tests.Scripting;

public class ScriptOutputPatternTests
{
	[Theory]
	[InlineData("@@*@@", 1)]
	[InlineData("*", 1)]
	[InlineData("\\*", 0)]
	[InlineData("a\\*b*", 1)]
	[InlineData("@@\\*@@", 0)]
	[InlineData("@@\\*@@*@@", 1)]
	[InlineData("no marker", 0)]
	[InlineData("", 0)]
	public void CountUnescapedStars_CountsMarkersOnly(string pattern, int expected)
		=> Assert.Equal(expected, ScriptOutputPattern.CountUnescapedStars(pattern));

	[Fact]
	public void TrySplit_ResolvesEscapedAsterisks()
	{
		Assert.True(ScriptOutputPattern.TrySplit("a\\*b*c", out var begin, out var end));
		Assert.Equal("a*b", begin);
		Assert.Equal("c", end);
	}

	[Fact]
	public void TrySplit_UnescapesALiteralAsteriskInsideAMarker()
	{
		Assert.True(ScriptOutputPattern.TrySplit("@@\\*@@*@@", out var begin, out var end));
		Assert.Equal("@@*@@", begin);
		Assert.Equal("@@", end);
	}

	[Fact]
	public void TrySplit_RejectsAPatternWithoutAMarker()
		=> Assert.False(ScriptOutputPattern.TrySplit("plain", out _, out _));

	[Fact]
	public void Extract_Whole_ParsesTheTrimmedOutput()
	{
		var json = ScriptOutputPattern.Extract("  { \"a\": 1 }\n", ScriptOutputMode.Whole, null, out var error);

		Assert.Null(error);
		Assert.Equal(1, (int)json!["a"]!);
	}

	[Fact]
	public void Extract_Pattern_TakesThePayloadOutOfNoisyOutput()
	{
		const string stdout = "noise\n@@\n{\n  \"title\": \"hi\"\n}\n@@\nmore noise";

		var json = ScriptOutputPattern.Extract(stdout, ScriptOutputMode.Pattern, "@@*@@", out var error);

		Assert.Null(error);
		Assert.Equal("hi", (string)json!["title"]!);
	}

	[Fact]
	public void Extract_Pattern_IsGreedyBetweenTheMarkers()
	{
		const string stdout = "<END> some logs... <START>{ \"result\": \"ok<END>\" }<END>";

		var json = ScriptOutputPattern.Extract(stdout, ScriptOutputMode.Pattern, "<START>*<END>", out var error);

		Assert.Null(error);
		Assert.Equal("ok<END>", (string)json!["result"]!);
	}

	[Fact]
	public void Extract_Pattern_ReportsAMissingBeginMarker()
	{
		Assert.Null(ScriptOutputPattern.Extract("nothing here", ScriptOutputMode.Pattern, "@@*@@", out var error));
		Assert.Contains("@@", error);
	}

	[Fact]
	public void Extract_Pattern_ReportsAMissingEndMarker()
	{
		Assert.Null(ScriptOutputPattern.Extract("@@ {}", ScriptOutputMode.Pattern, "@@*@@", out var error));
		Assert.Contains("was not found", error);
	}

	[Fact]
	public void Extract_Pattern_ReportsInvalidJson()
	{
		Assert.Null(ScriptOutputPattern.Extract("@@ not json @@", ScriptOutputMode.Pattern, "@@*@@", out var error));
		Assert.Contains("does not parse", error);
	}

	[Fact]
	public void Extract_Pattern_ReportsAMissingPattern()
	{
		Assert.Null(ScriptOutputPattern.Extract("{}", ScriptOutputMode.Pattern, null, out var error));
		Assert.Contains("no pattern", error);
	}

	[Fact]
	public void Extract_Whole_NeverDegradesIntoText()
	{
		Assert.Null(ScriptOutputPattern.Extract("plain text", ScriptOutputMode.Whole, null, out var error));
		Assert.NotNull(error);
	}
}
