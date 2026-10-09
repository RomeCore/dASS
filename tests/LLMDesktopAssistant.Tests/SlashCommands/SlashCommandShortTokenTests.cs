using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The shortest unambiguous form of a command's token: namespaces are dropped only while the resolver still picks
	/// the very same command.
	/// </summary>
	public class SlashCommandShortTokenTests
	{
		private static SlashCommandInfo Command(string name, params string[] namespaces)
			=> new() { Name = name, Namespaces = [.. namespaces] };

		[Fact]
		public void For_AnUncontestedName_DropsEveryNamespace()
		{
			var command = Command("web-searcher", "agent", ".claude");

			Assert.Equal("web-searcher", SlashCommandShortToken.For(command, [command]));
		}

		[Fact]
		public void For_ACommandWithoutNamespaces_IsItsName()
		{
			var command = Command("clear");

			Assert.Equal("clear", SlashCommandShortToken.For(command, [command]));
		}

		[Fact]
		public void For_ANameSharedWithAnother_OnlyTheWinnerMayDropIt()
		{
			// The names tie, so the resolver's last tie-break — the ordinal key — decides: ".claude:web-searcher" sorts before
			// "agent:web-searcher" ('.' < 'a'), and only that one may then be written without its namespace.
			var winner = Command("web-searcher", ".claude");
			var loser = Command("web-searcher", "agent");

			Assert.Equal("web-searcher", SlashCommandShortToken.For(winner, [winner, loser]));
			Assert.Equal("agent:web-searcher", SlashCommandShortToken.For(loser, [winner, loser]));
		}

		[Fact]
		public void For_KeepsTheShortestSuffixThatStillResolves()
		{
			// The pack segment has to stay: the bare name belongs to the other command.
			var mine = Command("grilling", "skill", "matt-pocock");
			var other = Command("grilling", "agent");

			Assert.Equal("matt-pocock:grilling", SlashCommandShortToken.For(mine, [mine, other]));
			Assert.Equal("grilling", SlashCommandShortToken.For(other, [mine, other]));
		}
	}
}
