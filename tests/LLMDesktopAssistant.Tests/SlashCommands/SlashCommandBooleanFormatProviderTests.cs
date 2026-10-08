using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The boolean argument format provider: validation, conversion and completion of <c>true</c> / <c>false</c>.
	/// </summary>
	public class SlashCommandBooleanFormatProviderTests
	{
		private static SlashCommandCompletionContext Context() => new()
		{
			Command = new SlashCommandInfo { Name = "web-searcher", Namespaces = ["agent"] },
			RawArguments = string.Empty,
			CurrentPrefix = string.Empty
		};

		[Theory]
		[InlineData("true")]
		[InlineData("false")]
		[InlineData("True")]
		[InlineData("FALSE")]
		public void TryValidate_AcceptsBooleanText(string raw)
			=> Assert.True(SlashCommandBooleanFormatProvider.Instance.TryValidate(raw, out _));

		[Theory]
		[InlineData("yes")]
		[InlineData("1")]
		[InlineData("")]
		public void TryValidate_RejectsAnythingElse(string raw)
		{
			Assert.False(SlashCommandBooleanFormatProvider.Instance.TryValidate(raw, out var error));
			Assert.Equal("command.error.invalid_argument", error!.Key);
		}

		[Fact]
		public void Convert_ReturnsABoolean()
			=> Assert.True(Assert.IsType<bool>(SlashCommandBooleanFormatProvider.Instance.Convert("true")));

		[Fact]
		public void Complete_WithoutAPrefix_OffersBothValues()
		{
			var values = SlashCommandBooleanFormatProvider.Instance.Complete(string.Empty, Context())
				.Select(item => item.Value)
				.ToList();

			Assert.Equal(new[] { "true", "false" }, values);
		}

		[Fact]
		public void Complete_MatchesThePrefix()
		{
			var values = SlashCommandBooleanFormatProvider.Instance.Complete("tr", Context())
				.Select(item => item.Value)
				.ToList();

			Assert.Equal(new[] { "true" }, values);
		}
	}
}
