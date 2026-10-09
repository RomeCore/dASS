using System.Collections.Generic;
using System.Collections.Immutable;
using Avalonia.Media;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The slash-command renderer — the painting half of <see cref="SlashCommandCompletionSource"/>: the token palette,
	/// the argument chips and the fallback for an argument list that does not parse.
	/// </summary>
	public class SlashCommandCompletionRendererTests
	{
		/// <summary>The chat's command set, as the source expects it.</summary>
		private sealed class Collector(IEnumerable<SlashCommandInfo> commands) : IAddonSetCollector<SlashCommandInfo>
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

		private static SlashCommandArgument Argument(string name) => new() { Name = Locale.GetKey(name) };

		/// <summary>A command whose whole argument region is one rest positional.</summary>
		private static readonly SlashCommandInfo Grilling = new()
		{
			Name = "grilling",
			Namespaces = ["skill"],
			Executor = new FakeExecutor(new SlashCommandArgumentSchema { RestPositional = Argument("test.rest") })
		};

		private static readonly SlashCommandInfo AgentGrilling = new() { Name = "grilling", Namespaces = ["agent"] };

		/// <summary>A command with one declared positional, so an unterminated quote is a parse error.</summary>
		private static readonly SlashCommandInfo Plain = new()
		{
			Name = "plain",
			Namespaces = ["skill"],
			Executor = new FakeExecutor(new SlashCommandArgumentSchema { Positionals = [Argument("test.one")] })
		};

		/// <summary>A command with a rest positional and a keyed argument.</summary>
		private static readonly SlashCommandInfo Agent = new()
		{
			Name = "agent",
			Namespaces = ["agent"],
			Executor = new FakeExecutor(new SlashCommandArgumentSchema
			{
				RestPositional = Argument("test.rest"),
				Keyed = new Dictionary<string, SlashCommandArgument>
				{
					["wait"] = Argument("test.wait")
				}.ToImmutableDictionary()
			})
		};

		private static IReadOnlyList<TextHighlightSpan>? Render(string text, params SlashCommandInfo[] commands)
			=> new SlashCommandCompletionSource(new Collector(commands)).TryHighlight(text, text.Length);

		/// <summary>Renders and requires that the source drew something.</summary>
		private static IReadOnlyList<TextHighlightSpan> Rendered(string text, params SlashCommandInfo[] commands)
		{
			var spans = Render(text, commands);

			Assert.NotNull(spans);
			return spans;
		}

		private static void AssertChipped(TextHighlightSpan span, int start, int length, IBrush brush)
		{
			Assert.Equal(start, span.Start);
			Assert.Equal(length, span.Length);
			Assert.Same(brush, span.Brush);
			Assert.Same(SlashCommandHighlightPalette.Default.ArgumentBackground, span.Background);
		}

		[Fact]
		public void Render_PlainText_DrawsNothing()
		{
			Assert.Null(Render("hello there", Grilling));
		}

		[Fact]
		public void Render_AnUnknownToken_IsPaintedWithTheUnknownBrush_AndUnderlined()
		{
			var span = Assert.Single(Rendered("/zzz", Grilling));

			Assert.Same(SlashCommandHighlightPalette.Default.Unknown, span.Brush);
			Assert.Same(SlashCommandHighlightPalette.Default.UnknownDecorations, span.Decorations);
		}

		[Fact]
		public void Render_ATokenThatWonOverOthers_IsAmbiguous_AndUnderlined()
		{
			var span = Assert.Single(Rendered("/grilling", Grilling, AgentGrilling));

			Assert.Same(SlashCommandHighlightPalette.Default.Ambiguous, span.Brush);
			Assert.Same(SlashCommandHighlightPalette.Default.AmbiguousDecorations, span.Decorations);
		}

		[Fact]
		public void Render_APartialToken_DrawsNothing()
		{
			// Nothing has resolved yet, so there is nothing to say about it.
			Assert.Null(Render("/gr", Grilling));
		}

		[Fact]
		public void Render_AKnownToken_IsPaintedWithTheKnownBrush()
		{
			var span = Assert.Single(Rendered("/grilling", Grilling));

			Assert.Equal(0, span.Start);
			Assert.Equal(9, span.Length);
			Assert.Same(SlashCommandHighlightPalette.Default.Known, span.Brush);
			Assert.Null(span.Decorations);
		}

		[Fact]
		public void Render_AnArgument_IsPaintedAsOneChippedUnit()
		{
			var spans = Rendered("/grilling do it", Grilling);

			Assert.Equal(2, spans.Count);
			Assert.Same(SlashCommandHighlightPalette.Default.Known, spans[0].Brush);
			AssertChipped(spans[1], 10, 5, SlashCommandHighlightPalette.Default.Argument);
		}

		[Fact]
		public void Render_AKeyedArgument_PaintsItsNameItsEqualsItsValue_AndTheQuotes()
		{
			// "/agent wait=\"true\"": the key [7,4), the '=' [11,1), the quotes and the value inside them.
			var spans = Rendered("/agent wait=\"true\"", Agent);

			Assert.Equal(6, spans.Count);
			Assert.Same(SlashCommandHighlightPalette.Default.Known, spans[0].Brush);
			AssertChipped(spans[1], 7, 4, SlashCommandHighlightPalette.Default.ArgumentKey);
			AssertChipped(spans[2], 11, 1, SlashCommandHighlightPalette.Default.ArgumentEquals);
			AssertChipped(spans[3], 12, 1, SlashCommandHighlightPalette.Default.Quote);
			AssertChipped(spans[4], 13, 4, SlashCommandHighlightPalette.Default.Argument);
			AssertChipped(spans[5], 17, 1, SlashCommandHighlightPalette.Default.Quote);
		}

		[Fact]
		public void Render_ASingleQuotedArgument_HasItsQuotesPaintedLighter()
		{
			// "/grilling 'do it'": one positional, its grouping quotes set apart from the value.
			var spans = Rendered("/grilling 'do it'", Grilling);

			Assert.Equal(4, spans.Count);
			AssertChipped(spans[1], 10, 1, SlashCommandHighlightPalette.Default.Quote);
			AssertChipped(spans[2], 11, 5, SlashCommandHighlightPalette.Default.Argument);
			AssertChipped(spans[3], 16, 1, SlashCommandHighlightPalette.Default.Quote);
		}

		[Fact]
		public void Render_AnUnparseableArgumentList_FallsBackToThePlainArgumentColour()
		{
			// The quote of a *declared* positional is never closed, so the list does not parse (a rest positional would
			// have taken it verbatim): the region stays one grey span, with no chips.
			var spans = Rendered("/plain \"do it", Plain);

			Assert.Equal(2, spans.Count);
			Assert.Equal(new TextHighlightSpan(7, 6, SlashCommandHighlightPalette.Default.Argument), spans[1]);
		}

		[Fact]
		public void Render_OnlyTheLeadingTokenIsACommand_Multiline()
		{
			var spans = Rendered("/grilling\nsecond /nope", Grilling);

			// The token, then the whole rest (arguments including the second line) — never a second token.
			Assert.Equal(2, spans.Count);
			Assert.Equal(0, spans[0].Start);
			Assert.Equal(9, spans[0].Length);
			Assert.Equal(10, spans[1].Start);
		}

		[Fact]
		public void Render_WithNoCommands_DrawsNothing()
		{
			Assert.Null(Render("/grilling do it"));
		}
	}
}
