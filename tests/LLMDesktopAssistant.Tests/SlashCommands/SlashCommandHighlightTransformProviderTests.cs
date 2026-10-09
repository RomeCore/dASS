using System.Collections.Generic;
using System.Collections.Immutable;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The slash-command renderer: the token palette, the argument chips and the ghost preview. It is a pure projection
	/// of the given completion, so the tests drive it with hand-made results.
	/// </summary>
	public class SlashCommandHighlightTransformProviderTests
	{
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

		private InputCompletionResult? _completion;

		private bool _enabled = true;

		// Defaults to the end of the text (the append case); a test can set it to a mid-token offset.
		private int _caretIndex = int.MaxValue;

		private SlashCommandHighlightTransformProvider Provider(params SlashCommandInfo[] commands)
			=> new(
				() => _enabled ? commands : [],
				() => _caretIndex,
				() => _completion,
				SlashCommandHighlightPalette.Default);

		private HighlightTransformResult Transform(string text, params SlashCommandInfo[] commands)
			=> Provider(commands).Transform(text);

		private static TextHighlightSpan Single(HighlightTransformResult result) => Assert.Single(result.HighlightSpans!);

		private static void AssertChipped(TextHighlightSpan span, int start, int length, IBrush brush,
			SlashCommandHighlightPalette? palette = null)
		{
			palette ??= SlashCommandHighlightPalette.Default;

			Assert.Equal(start, span.Start);
			Assert.Equal(length, span.Length);
			Assert.Same(brush, span.Brush);
			Assert.Same(palette.ArgumentBackground, span.Background);
		}

		[Fact]
		public void Transform_PlainText_HasNoSpans()
		{
			var result = Transform("hello there", Grilling);

			Assert.Null(result.HighlightSpans);
			Assert.Null(result.RenderedText);
		}

		[Fact]
		public void Transform_AKnownToken_IsPaintedWithTheKnownBrush()
		{
			var span = Single(Transform("/grilling", Grilling));

			Assert.Equal(new TextHighlightSpan(0, 9, SlashCommandHighlightPalette.Default.Known),
				span with { Decorations = null });
			Assert.Null(span.Decorations);
		}

		[Fact]
		public void Transform_AnUnknownToken_IsPaintedWithTheUnknownBrush_AndUnderlined()
		{
			var span = Single(Transform("/zzz", Grilling));

			Assert.Same(SlashCommandHighlightPalette.Default.Unknown, span.Brush);
			Assert.Same(SlashCommandHighlightPalette.Default.UnknownDecorations, span.Decorations);
		}

		[Fact]
		public void Transform_ATokenThatWonOverOthers_IsAmbiguous_AndUnderlined()
		{
			var span = Single(Transform("/grilling", Grilling, AgentGrilling));

			Assert.Same(SlashCommandHighlightPalette.Default.Ambiguous, span.Brush);
			Assert.Same(SlashCommandHighlightPalette.Default.AmbiguousDecorations, span.Decorations);
		}

		[Fact]
		public void Transform_APartialToken_IsNotPainted()
		{
			var result = Transform("/gr", Grilling);

			Assert.Null(result.HighlightSpans);
		}

		[Fact]
		public void Transform_AnArgument_IsPaintedAsOneChippedUnit()
		{
			var spans = Transform("/grilling do it", Grilling).HighlightSpans!;

			Assert.Equal(2, spans.Count);
			Assert.Equal(new TextHighlightSpan(0, 9, SlashCommandHighlightPalette.Default.Known),
				spans[0] with { Decorations = null });
			AssertChipped(spans[1], 10, 5, SlashCommandHighlightPalette.Default.Argument);
		}

		[Fact]
		public void Transform_AKeyedArgument_PaintsItsNameItsEqualsItsValue_AndTheQuotes()
		{
			// "/agent wait=\"true\"": the key [7,4), the '=' [11,1), the quotes and the value inside them.
			var spans = Transform("/agent wait=\"true\"", Agent).HighlightSpans!;

			Assert.Equal(6, spans.Count);
			Assert.Equal(new TextHighlightSpan(0, 6, SlashCommandHighlightPalette.Default.Known),
				spans[0] with { Decorations = null });
			AssertChipped(spans[1], 7, 4, SlashCommandHighlightPalette.Default.ArgumentKey);
			AssertChipped(spans[2], 11, 1, SlashCommandHighlightPalette.Default.ArgumentEquals);
			AssertChipped(spans[3], 12, 1, SlashCommandHighlightPalette.Default.Quote);
			AssertChipped(spans[4], 13, 4, SlashCommandHighlightPalette.Default.Argument);
			AssertChipped(spans[5], 17, 1, SlashCommandHighlightPalette.Default.Quote);
		}

		[Fact]
		public void Transform_ASingleQuotedArgument_HasItsQuotesPaintedLighter()
		{
			// "/grilling 'do it'": one positional, its grouping quotes set apart from the value.
			var spans = Transform("/grilling 'do it'", Grilling).HighlightSpans!;

			Assert.Equal(4, spans.Count);
			AssertChipped(spans[1], 10, 1, SlashCommandHighlightPalette.Default.Quote);
			AssertChipped(spans[2], 11, 5, SlashCommandHighlightPalette.Default.Argument);
			AssertChipped(spans[3], 16, 1, SlashCommandHighlightPalette.Default.Quote);
		}

		[Fact]
		public void Transform_AnUnparseableArgumentList_FallsBackToThePlainArgumentColour()
		{
			// The quote of a *declared* positional is never closed, so the list does not parse (a rest positional would
			// have taken it verbatim): the region stays one grey span, with no chips.
			var spans = Transform("/plain \"do it", Plain).HighlightSpans!;

			Assert.Equal(2, spans.Count);
			Assert.Equal(new TextHighlightSpan(7, 6, SlashCommandHighlightPalette.Default.Argument), spans[1]);
		}

		[Fact]
		public void Transform_OnlyTheLeadingTokenIsACommand_Multiline()
		{
			var spans = Transform("/grilling\nsecond /nope", Grilling).HighlightSpans!;

			// The token, then the whole rest (arguments including the second line) — never a second token.
			Assert.Equal(2, spans.Count);
			Assert.Equal(0, spans[0].Start);
			Assert.Equal(9, spans[0].Length);
			Assert.Equal(10, spans[1].Start);
		}

		[Fact]
		public void Transform_ACompletionAtTheRegionEnd_RendersTheGhost_AndPaintsIt()
		{
			_completion = new InputCompletionResult
			{
				Span = new InputCompletionSpan(0, 3),
				GhostText = "illing"
			};

			var result = Transform("/gr", Grilling);

			Assert.Equal("/grilling", result.RenderedText);
			var span = Single(result);
			Assert.Equal(new TextHighlightSpan(3, 6, SlashCommandHighlightPalette.Default.Ghost), span);
		}

		[Fact]
		public void Transform_TheGhostReplacesTheRegionTail_FromTheCaret_AndIsExposedForAcceptance()
		{
			// The completion would replace "abc" entirely, so the preview keeps "a" and drops the rest of the region
			// instead of drawing it beside the ghost.
			_completion = new InputCompletionResult { Span = new InputCompletionSpan(0, 4), GhostText = "XYZ" };
			_caretIndex = 1;

			var provider = Provider(Grilling);
			var result = provider.Transform("abc");

			Assert.Equal("aXYZ", result.RenderedText);
			Assert.Equal(new TextHighlightSpan(1, 3, SlashCommandHighlightPalette.Default.Ghost), result.HighlightSpans![^1]);
			Assert.Equal("XYZ", provider.CompletionText);
			Assert.Equal(4, provider.TokenEnd);
		}

		[Fact]
		public void Transform_TheGhostReplacesTheRegionTail_KeepingWhatFollowsTheRegion()
		{
			// "wait=t|r": the ghost "rue" completes the value, so the "r" under the caret must not survive beside it —
			// and everything after the region (" wait=false") does.
			_completion = new InputCompletionResult { Span = new InputCompletionSpan(11, 2), GhostText = "rue" };
			_caretIndex = 12;

			var result = Transform("agent wait=tr wait=false", Grilling);

			Assert.Equal("agent wait=true wait=false", result.RenderedText);
		}

		[Fact]
		public void Transform_ACompletionWithNoGhostText_DoesNotChangeTheRenderedText()
		{
			_completion = new InputCompletionResult { Span = new InputCompletionSpan(0, 3) };

			var result = Transform("/grilling", Grilling);

			Assert.Null(result.RenderedText);
		}

		[Fact]
		public void Transform_WhenDisabled_HasNoSpans_AndNoGhost()
		{
			_enabled = false;
			_completion = new InputCompletionResult { Span = new InputCompletionSpan(0, 3), GhostText = "illing" };

			var result = Transform("/grilling", Grilling);

			Assert.Null(result.HighlightSpans);
			Assert.Null(result.RenderedText);
		}
	}
}
