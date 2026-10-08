using LLMDesktopAssistant.SlashCommands;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The message-level command extraction: the leading "/" marker and the "//" escape.
	/// </summary>
	public class SlashCommandExtractorTests
	{
		[Theory]
		[InlineData("hello world", false, "", "")]
		[InlineData("/name", true, "name", "")]
		[InlineData("/name args", true, "name", "args")]
		[InlineData("  /name spaced", true, "name", "spaced")]
		[InlineData("/name    padded", true, "name", "padded")]
		[InlineData("/", true, "", "")]
		[InlineData("//escaped", false, "", "")]
		[InlineData("   //escaped", false, "", "")]
		[InlineData("", false, "", "")]
		public void TryExtractToken_Cases(string text, bool expected, string token, string arguments)
		{
			var result = SlashCommandExtractor.TryExtractToken(text, out var actualToken, out var actualArguments);

			Assert.Equal(expected, result);
			Assert.Equal(token, actualToken);
			Assert.Equal(arguments, actualArguments);
		}

		[Fact]
		public void TryExtractToken_KeepsAMultiLineRemainder()
		{
			Assert.True(SlashCommandExtractor.TryExtractToken("/name first\nsecond third", out var token, out var args));

			Assert.Equal("name", token);
			Assert.Equal("first\nsecond third", args);
		}

		[Theory]
		[InlineData("//foo", "/foo")]
		[InlineData("//", "/")]
		[InlineData("  //foo", "  /foo")]
		[InlineData("/foo", "/foo")]
		[InlineData("plain", "plain")]
		public void UnescapeLeadingSlash_Cases(string text, string expected)
			=> Assert.Equal(expected, SlashCommandExtractor.UnescapeLeadingSlash(text));
	}
}
