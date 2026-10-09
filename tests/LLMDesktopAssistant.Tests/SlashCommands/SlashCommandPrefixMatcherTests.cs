using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The pure prefix matcher: which commands a partially typed token could still become.
	/// </summary>
	public class SlashCommandPrefixMatcherTests
	{
		private static SlashCommandInfo Command(string name, string[] namespaces, string[]? aliases = null) =>
			new() { Name = name, Namespaces = [.. namespaces], Aliases = [.. aliases ?? []] };

		private static readonly SlashCommandInfo Grilling =
			Command("grilling", ["skill", "matt-pocock"], ["grill"]);

		private static readonly SlashCommandInfo WebSearcher =
			Command("web-searcher", ["agent"]);

		private static readonly SlashCommandInfo[] Commands = [Grilling, WebSearcher];

		private static string[] MatchedNames(string token)
			=> [.. SlashCommandPrefixMatcher.Match(Commands, token).Select(command => command.Name)];

		[Fact]
		public void Match_AnEmptyToken_MatchesEverything()
		{
			Assert.Equal(new[] { "grilling", "web-searcher" }, MatchedNames(""));
		}

		[Theory]
		[InlineData("gr")]
		[InlineData("gril")]
		[InlineData("gri")]
		public void Match_ANamePrefix_Matches(string token)
		{
			Assert.Equal(new[] { "grilling" }, MatchedNames(token));
		}

		[Fact]
		public void Match_AnAliasPrefix_Matches()
		{
			Assert.Equal(new[] { "grilling" }, MatchedNames("gril"));
		}

		[Fact]
		public void Match_IsCaseInsensitive()
		{
			Assert.Equal(new[] { "grilling" }, MatchedNames("GRILL"));
		}

		[Theory]
		[InlineData("skill:")]
		[InlineData("skill:gr")]
		[InlineData("matt-pocock:gr")]
		[InlineData("matt:gr")]
		[InlineData("matt:")]
		public void Match_NamespaceQualifiedPrefixes_Match(string token)
		{
			Assert.Equal(new[] { "grilling" }, MatchedNames(token));
		}

		[Fact]
		public void Match_AllNamespacesInAnyOrder_Match()
		{
			Assert.Equal(new[] { "grilling" }, MatchedNames("matt-pocock:skill:gr"));
		}

		[Fact]
		public void Match_ANonPrefix_DoesNotMatch()
		{
			Assert.Empty(MatchedNames("zzz"));
		}

		[Fact]
		public void Match_AMatchingNameFollowedByAnotherSegment_DoesNotMatch()
		{
			// No valid token is "grilling:extra" — the name must come last.
			Assert.Empty(MatchedNames("grilling:extra"));
		}

		[Fact]
		public void Match_AnUnknownNamespace_DoesNotMatch()
		{
			Assert.Empty(MatchedNames("nope:gr"));
		}
	}
}
