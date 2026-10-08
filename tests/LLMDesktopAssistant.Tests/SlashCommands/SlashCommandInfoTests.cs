using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The canonical slash-free token of a command.
	/// </summary>
	public class SlashCommandInfoTests
	{
		private static SlashCommandInfo Command(params string[] namespaces) => new()
		{
			Name = "grilling",
			Namespaces = [.. namespaces]
		};

		[Fact]
		public void CanonicalToken_JoinsNamespacesInStoredOrderAndTheName_SlashFree()
		{
			Assert.Equal("skill:matt-pocock:grilling", Command("skill", "matt-pocock").CanonicalToken);
			Assert.Equal("grilling", Command().CanonicalToken);
		}

		[Fact]
		public void CanonicalToken_KeepsStoredOrder_UnlikeTheKey()
		{
			var command = Command("skill", "matt-pocock");

			Assert.Equal("skill:matt-pocock:grilling", command.CanonicalToken);
			Assert.Equal("matt-pocock:skill:grilling", command.Key);
		}

		[Fact]
		public void CanonicalToken_IsReParseable()
		{
			var parsed = SlashCommandMatcher.ParseToken(Command("skill", "matt-pocock").CanonicalToken);

			Assert.True(parsed.IsValid);
			Assert.Equal("grilling", parsed.Name);
			Assert.Equal(new[] { "skill", "matt-pocock" }, parsed.Qualifiers);
		}
	}
}
