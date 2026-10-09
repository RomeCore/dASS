using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The slash-command completion source: the token state (qualified candidates, defeated marking) and the argument
	/// state (delegated to the slot's format provider). The ghost is derived from the result's selected item.
	/// </summary>
	public class SlashCommandCompletionSourceTests
	{
		private sealed class FakeCollector(IEnumerable<SlashCommandInfo> commands) : IAddonSetCollector<SlashCommandInfo>
		{
			public IEnumerable<SlashCommandInfo> GetAvailableAddons() => commands;
			public IEnumerable<SlashCommandInfo> GetAddonsForChat() => commands;
			public IEnumerable<SlashCommandInfo> GetAddonsForAgent(ChatAgentDescriptor agent) => commands;
		}

		private sealed class FakeExecutor(SlashCommandArgumentSchema? schema) : ISlashCommandExecutor
		{
			public SlashCommandArgumentSchema? ArgumentSchema { get; } = schema;

			public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
				=> Task.FromResult(SlashCommandExecutionResult.Ok());
		}

		private static SlashCommandArgumentSchema WaitSchema() => new()
		{
			RestPositional = new SlashCommandArgument { Name = Locale.GetKey("command.argument.rest") },
			Keyed = new Dictionary<string, SlashCommandArgument>
			{
				["wait"] = new()
				{
					Name = Locale.GetKey("command.argument.wait"),
					Description = Locale.GetKey("command.argument.wait.description"),
					Format = SlashCommandBooleanFormatProvider.Instance
				}
			}.ToImmutableDictionary()
		};

		private static SlashCommandInfo Command(string name, params string[] namespaces) => new()
		{
			Name = name,
			Namespaces = [.. namespaces]
		};

		private static readonly SlashCommandInfo SkillGrilling = Command("grilling", "skill");
		private static readonly SlashCommandInfo AgentGrilling = Command("grilling", "agent");
		private static readonly SlashCommandInfo WebSearcher = new()
		{
			Name = "web-searcher",
			Namespaces = ["agent"],
			Executor = new FakeExecutor(WaitSchema())
		};

		// A declared positional with no format provider — a slot that yields a state-only result.
		private static readonly SlashCommandInfo Plain = new()
		{
			Name = "plain",
			Namespaces = ["skill"],
			Executor = new FakeExecutor(new SlashCommandArgumentSchema
			{
				Positionals =
				[
					new SlashCommandArgument { Name = Locale.GetKey("command.argument.thing"), Required = true }
				]
			})
		};

		private static SlashCommandCompletionSource Source(params SlashCommandInfo[] commands)
			=> new(new FakeCollector(commands));

		[Fact]
		public void TryCompute_PlainText_ReturnsNull()
		{
			Assert.Null(Source(SkillGrilling).TryCompute("hello", 5));
		}

		[Fact]
		public void TryCompute_WithNoCommands_ReturnsNull()
		{
			Assert.Null(Source().TryCompute("/gr", 3));
		}

		[Fact]
		public void TryCompute_WithTheCaretBeforeTheToken_ReturnsNull()
		{
			Assert.Null(Source(SkillGrilling).TryCompute("  /gr", 0));
		}

		[Fact]
		public void TryCompute_ATokenPrefix_ReturnsQualifiedCandidates_AndMarksDefeated()
		{
			var result = Source(SkillGrilling, AgentGrilling).TryCompute("/gr", 3);

			Assert.NotNull(result);
			Assert.Equal(new InputCompletionSpan(0, 3), result!.Span);
			Assert.NotNull(result.State);
			Assert.Equal("command.completion.title.commands", result.State!.Title!.Key);
			Assert.Empty(result.State.ContextItems);
			Assert.Equal(2, result.Items.Count);

			// Ordered by name then key: both are "grilling", so agent:grilling wins the tie and skill:grilling is defeated. Only
			// the winner may drop its namespace — the loser keeps the qualifier that tells it apart, so the collapsed form is
			// never ambiguous.
			Assert.Equal("/grilling", result.Items[0].InsertText);
			Assert.Equal("/agent:grilling", result.Items[0].Hint);
			Assert.False(result.Items[0].IsDefeated);
			Assert.Equal("/skill:grilling", result.Items[1].InsertText);
			Assert.Null(result.Items[1].Hint);
			Assert.True(result.Items[1].IsDefeated);
		}

		[Fact]
		public void TryCompute_ANamespaceQualifiedPrefix_ReturnsItsCandidates()
		{
			var result = Source(SkillGrilling, AgentGrilling).TryCompute("/skill:", 7);

			Assert.NotNull(result);
			var item = Assert.Single(result!.Items);
			Assert.Equal("/skill:grilling", item.InsertText);
		}

		[Fact]
		public void TryCompute_AnUnknownToken_ReturnsAnEmptyStateOnlyResult()
		{
			var result = Source(SkillGrilling).TryCompute("/zzz", 4);

			Assert.NotNull(result);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);
		}

		[Fact]
		public void TryCompute_AnArgumentWithAFormatProvider_ReturnsItsCompletions()
		{
			var result = Source(WebSearcher).TryCompute("/agent:web-searcher wait=", 25);

			Assert.NotNull(result);
			Assert.Equal(new[] { "true", "false" }, result!.Items.Select(item => item.InsertText));
			Assert.Equal(InputCompletionKind.Argument, result.State!.Kind);
		}

		[Fact]
		public void TryCompute_ACaretInTheMiddleOfAnArgumentValue_SpansTheValue_AndUsesThePrefix()
		{
			// "/agent:web-searcher wait=tr" — the value is at [25,27), the prefix is "tr".
			var result = Source(WebSearcher).TryCompute("/agent:web-searcher wait=tr", 27);

			Assert.NotNull(result);
			Assert.Equal(new InputCompletionSpan(25, 2), result!.Span);
			var item = Assert.Single(result.Items);
			Assert.Equal("true", item.InsertText);
		}

		[Fact]
		public void TryCompute_AnArgumentWithoutAProvider_ReturnsAStateOnlyResult()
		{
			// "foo" fills the declared positional, which declares no format provider.
			var result = Source(Plain).TryCompute("/skill:plain foo", 16);

			Assert.NotNull(result);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);

			// The required argument is listed, and marked as the one the caret sits in.
			var argument = Assert.Single(result.State!.ContextItems);
			Assert.True(argument.IsRequired);
			Assert.True(argument.IsCurrent);
		}

		[Fact]
		public void TryCompute_TheRestPositional_ReturnsAStateOnlyResult()
		{
			// The whole remainder is the rest positional: a real slot with no format provider — nothing to complete, but
			// the caret inside it is accounted for.
			var result = Source(WebSearcher).TryCompute("/agent:web-searcher x", 21);

			Assert.NotNull(result);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);
		}

		[Fact]
		public void TryCompute_AnArgument_DescribesTheCommand_AndListsItsArguments()
		{
			// The caret sits in the free text, i.e. in the rest positional, with "wait" declared besides it.
			var result = Source(WebSearcher).TryCompute("/agent:web-searcher Погода", 25);

			Assert.NotNull(result);
			var state = result!.State!;
			Assert.Equal("/web-searcher", state.Title!.Key);
			Assert.Same(WebSearcher.DescriptionKey, state.Description);
			Assert.Equal("command.argument.rest", state.ContextTitle!.Key);

			Assert.Equal(2, state.ContextItems.Count);
			Assert.Equal("command.argument.rest", state.ContextItems[0].Name.Key);
			Assert.True(state.ContextItems[0].IsCurrent);
			Assert.False(state.ContextItems[0].IsRequired);
			Assert.Equal("command.argument.wait", state.ContextItems[1].Name.Key);
			Assert.False(state.ContextItems[1].IsCurrent);
		}

		[Fact]
		public void TryCompute_InAKeyedValue_NamesTheSlotAsTheContext()
		{
			// The caret sits after the value of "wait", so that keyed argument is the one under it.
			var result = Source(WebSearcher).TryCompute("/agent:web-searcher wait=t", 26);

			Assert.NotNull(result);
			var state = result!.State!;
			Assert.Equal("command.argument.wait", state.ContextTitle!.Key);
			Assert.Equal("command.argument.wait.description", state.ContextDescription!.Key);
			Assert.True(state.ContextItems[1].IsCurrent);
		}

		[Fact]
		public void TryCompute_ATokenTheMatchContinues_PreviewsTheMissingSuffix()
		{
			var result = Source(SkillGrilling).TryCompute("/gri", 4);

			Assert.NotNull(result);
			Assert.Equal("/grilling", result!.Items[0].InsertText);
			Assert.Equal("/skill:grilling", result.Items[0].Hint);
			Assert.Equal("lling", result.GhostOf(result.Items[0]));
		}

		[Fact]
		public void TryCompute_AMidTokenCaret_PreviewsFromTheCaret()
		{
			var result = Source(SkillGrilling).TryCompute("/grilling", 3);

			Assert.NotNull(result);
			Assert.Equal("illing", result!.GhostOf(result.Items[0]));
		}

		[Fact]
		public void TryCompute_ATokenTheMatchDoesNotContinue_HasNoGhost()
		{
			// The token matched by its namespace, so the offered form ("/grilling") does not extend what is typed.
			var result = Source(SkillGrilling).TryCompute("/sk", 3);

			Assert.NotNull(result);
			Assert.Equal("/grilling", result!.Items[0].InsertText);
			Assert.Null(result.GhostOf(result.Items[0]));
		}

		[Fact]
		public void TryCompute_ArgumentModeWhenTheTokenDoesNotResolve_ReturnsNull()
		{
			Assert.Null(Source(WebSearcher).TryCompute("/zzz x", 6));
		}
	}
}
