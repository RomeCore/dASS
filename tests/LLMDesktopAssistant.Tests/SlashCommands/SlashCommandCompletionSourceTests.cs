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
	/// state (delegated to the slot's format provider).
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
		public void TryCompute_PlainText_ReturnsFalse()
		{
			Assert.False(Source(SkillGrilling).TryCompute(new InputCompletionRequest("hello", 5), out _));
		}

		[Fact]
		public void TryCompute_WithNoCommands_ReturnsFalse()
		{
			Assert.False(Source().TryCompute(new InputCompletionRequest("/gr", 3), out _));
		}

		[Fact]
		public void TryCompute_WithTheCaretBeforeTheToken_ReturnsFalse()
		{
			Assert.False(Source(SkillGrilling).TryCompute(new InputCompletionRequest("  /gr", 0), out _));
		}

		[Fact]
		public void TryCompute_ATokenPrefix_ReturnsQualifiedCandidates_AndMarksDefeated()
		{
			var ok = Source(SkillGrilling, AgentGrilling).TryCompute(new InputCompletionRequest("/gr", 3), out var result);

			Assert.True(ok);
			Assert.Equal(new InputCompletionSpan(0, 3), result!.Span);
			Assert.NotNull(result.State);
			Assert.Equal("command.completion.title.commands", result.State!.Title!.Key);
			Assert.Empty(result.State.ContextItems);
			Assert.Equal(2, result.Items.Count);

			// Ordered by name then key: both are "grilling", so agent:grilling wins the tie and skill:grilling is defeated.
			Assert.Equal("/agent:grilling", result.Items[0].InsertText);
			Assert.False(result.Items[0].IsDefeated);
			Assert.Equal("/skill:grilling", result.Items[1].InsertText);
			Assert.True(result.Items[1].IsDefeated);
		}

		[Fact]
		public void TryCompute_ANamespaceQualifiedPrefix_ReturnsItsCandidates()
		{
			var ok = Source(SkillGrilling, AgentGrilling).TryCompute(new InputCompletionRequest("/skill:", 7), out var result);

			Assert.True(ok);
			var item = Assert.Single(result!.Items);
			Assert.Equal("/skill:grilling", item.InsertText);
		}

		[Fact]
		public void TryCompute_AnUnknownToken_ReturnsAnEmptyStateOnlyResult()
		{
			var ok = Source(SkillGrilling).TryCompute(new InputCompletionRequest("/zzz", 4), out var result);

			Assert.True(ok);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);
		}

		[Fact]
		public void TryCompute_AnArgumentWithAFormatProvider_ReturnsItsCompletions()
		{
			var ok = Source(WebSearcher).TryCompute(new InputCompletionRequest("/agent:web-searcher wait=", 25), out var result);

			Assert.True(ok);
			Assert.Equal(new[] { "true", "false" }, result!.Items.Select(item => item.InsertText));
			Assert.Equal(InputCompletionKind.Argument, result.State!.Kind);
		}

		[Fact]
		public void TryCompute_ACaretInTheMiddleOfAnArgumentValue_SpansTheValue_AndUsesThePrefix()
		{
			// "/agent:web-searcher wait=tr" — the value is at [25,27), the prefix is "tr".
			var ok = Source(WebSearcher).TryCompute(new InputCompletionRequest("/agent:web-searcher wait=tr", 27), out var result);

			Assert.True(ok);
			Assert.Equal(new InputCompletionSpan(25, 2), result!.Span);
			var item = Assert.Single(result.Items);
			Assert.Equal("true", item.InsertText);
		}

		[Fact]
		public void TryCompute_AnArgumentWithoutAProvider_ReturnsAStateOnlyResult()
		{
			// "foo" fills the declared positional, which declares no format provider.
			var ok = Source(Plain).TryCompute(new InputCompletionRequest("/skill:plain foo", 16), out var result);

			Assert.True(ok);
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
			var ok = Source(WebSearcher).TryCompute(new InputCompletionRequest("/agent:web-searcher x", 21), out var result);

			Assert.True(ok);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);
		}

		[Fact]
		public void TryCompute_AnArgument_DescribesTheCommand_AndListsItsArguments()
		{
			// The caret sits in the free text, i.e. in the rest positional, with "wait" declared besides it.
			var ok = Source(WebSearcher).TryCompute(new InputCompletionRequest("/agent:web-searcher Погода", 25), out var result);

			Assert.True(ok);
			var state = result!.State!;
			Assert.Equal("/agent:web-searcher", state.Title!.Key);
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
			var ok = Source(WebSearcher).TryCompute(new InputCompletionRequest("/agent:web-searcher wait=t", 26), out var result);

			Assert.True(ok);
			var state = result!.State!;
			Assert.Equal("command.argument.wait", state.ContextTitle!.Key);
			Assert.Equal("command.argument.wait.description", state.ContextDescription!.Key);
			Assert.True(state.ContextItems[1].IsCurrent);
		}

		[Fact]
		public void TryCompute_ArgumentModeWhenTheTokenDoesNotResolve_ReturnsFalse()
		{
			Assert.False(Source(WebSearcher).TryCompute(new InputCompletionRequest("/zzz x", 6), out _));
		}
	}
}
