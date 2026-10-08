using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The chat-scoped resolver: statuses, the error, and the defeated set.
	/// </summary>
	public class SlashCommandResolverTests
	{
		private sealed class FakeCollector(IEnumerable<SlashCommandInfo> commands) : IAddonSetCollector<SlashCommandInfo>
		{
			public IEnumerable<SlashCommandInfo> GetAvailableAddons() => commands;

			public IEnumerable<SlashCommandInfo> GetAddonsForChat() => commands;

			public IEnumerable<SlashCommandInfo> GetAddonsForAgent(ChatAgentDescriptor agent) => commands;
		}

		private static SlashCommandInfo Command(string name, string[] namespaces, int overrideOrder = 0) => new()
		{
			Name = name,
			Namespaces = [.. namespaces],
			OverrideOrder = overrideOrder
		};

		[Fact]
		public void Resolve_Unknown_WhenNothingMatches()
		{
			var resolver = new SlashCommandResolver(new FakeCollector([]));

			var resolution = resolver.Resolve("nope");

			Assert.Equal(SlashCommandResolutionStatus.Unknown, resolution.Status);
			Assert.Null(resolution.Command);
			Assert.Equal("command.error.unknown", resolution.Error!.Key);
			Assert.Empty(resolution.Defeated);
		}

		[Fact]
		public void Resolve_Unknown_WhenTheTokenIsInvalid()
		{
			var resolver = new SlashCommandResolver(new FakeCollector([Command("grilling", ["skill"])]));

			Assert.Equal(SlashCommandResolutionStatus.Unknown, resolver.Resolve("").Status);
			Assert.Equal(SlashCommandResolutionStatus.Unknown, resolver.Resolve("/").Status);
		}

		[Fact]
		public void Resolve_Exact_WhenOneCommandMatches()
		{
			var command = Command("grilling", ["skill"]);
			var resolver = new SlashCommandResolver(new FakeCollector([command]));

			var resolution = resolver.Resolve("skill:grilling");

			Assert.Equal(SlashCommandResolutionStatus.Exact, resolution.Status);
			Assert.Same(command, resolution.Command);
			Assert.Null(resolution.Error);
			Assert.Empty(resolution.Defeated);
		}

		[Fact]
		public void Resolve_WonOthers_CarriesTheDefeated()
		{
			var winner = Command("grilling", ["skill"], overrideOrder: 1);
			var loser = Command("grilling", ["agent"], overrideOrder: 0);
			var resolver = new SlashCommandResolver(new FakeCollector([winner, loser]));

			var resolution = resolver.Resolve("grilling");

			Assert.Equal(SlashCommandResolutionStatus.WonOthers, resolution.Status);
			Assert.Same(winner, resolution.Command);
			Assert.Equal(new[] { loser }, resolution.Defeated);
		}

		[Fact]
		public void Resolve_WonOthers_IncludesTheWinnersOverrides()
		{
			var hidden = Command("grilling", ["skill", "other-pack"]);
			var winner = Command("grilling", ["skill"], overrideOrder: 1);
			winner.Overrides = [hidden];
			var resolver = new SlashCommandResolver(new FakeCollector([winner]));

			var resolution = resolver.Resolve("grilling");

			Assert.Equal(SlashCommandResolutionStatus.WonOthers, resolution.Status);
			Assert.Equal(new[] { hidden }, resolution.Defeated);
		}
	}
}
