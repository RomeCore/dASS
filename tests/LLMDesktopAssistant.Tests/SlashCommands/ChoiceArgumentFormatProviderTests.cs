using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The "one of many" argument format provider: strict/non-strict validation and completion.
	/// </summary>
	public class ChoiceArgumentFormatProviderTests
	{
		private static SlashCommandCompletionContext Context() => new()
		{
			Command = new SlashCommandInfo { Name = "grilling", Namespaces = ["skill"] },
			RawArguments = string.Empty,
			CurrentPrefix = string.Empty
		};

		[Fact]
		public void AStrictProvider_ValidatesMembership()
		{
			var provider = new ChoiceArgumentFormatProvider(["low", "high"], strict: true);

			Assert.True(provider.TryValidate("low", out _));
			Assert.True(provider.TryValidate("HIGH", out _));
			Assert.False(provider.TryValidate("nope", out var error));
			Assert.Equal("command.error.invalid_argument", error!.Key);
		}

		[Fact]
		public void ANonStrictProvider_AcceptsAnything()
		{
			var provider = new ChoiceArgumentFormatProvider(["low", "high"], strict: false);

			Assert.True(provider.TryValidate("anything", out _));
		}

		[Fact]
		public void Convert_KeepsTheRawValue()
			=> Assert.Equal("low", new ChoiceArgumentFormatProvider(["low"], strict: true).Convert("low"));

		[Fact]
		public void Complete_MatchesThePrefix()
		{
			var provider = new ChoiceArgumentFormatProvider(["low", "medium", "high"], strict: true);

			Assert.Equal(["low", "medium", "high"],
				provider.Complete(string.Empty, Context()).Select(item => item.Value));

			Assert.Equal(["medium"],
				provider.Complete("me", Context()).Select(item => item.Value));
		}
	}
}
