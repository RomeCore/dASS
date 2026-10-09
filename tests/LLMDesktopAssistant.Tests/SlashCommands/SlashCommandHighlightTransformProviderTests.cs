using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The slash-command renderer: the token palette, the argument span and the ghost preview. It is a pure projection
	/// of the given completion, so the tests drive it with hand-made results.
	/// </summary>
	public class SlashCommandHighlightTransformProviderTests
	{
		private static readonly SlashCommandInfo Grilling = new() { Name = "grilling", Namespaces = ["skill"] };
		private static readonly SlashCommandInfo AgentGrilling = new() { Name = "grilling", Namespaces = ["agent"] };

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
		public void Transform_Arguments_ArePaintedGrey_AfterTheToken()
		{
			var spans = Transform("/grilling do it", Grilling).HighlightSpans!;

			Assert.Equal(2, spans.Count);
			Assert.Equal(new TextHighlightSpan(0, 9, SlashCommandHighlightPalette.Default.Known), spans[0] with { Decorations = null });
			Assert.Equal(new TextHighlightSpan(10, 5, SlashCommandHighlightPalette.Default.Argument), spans[1]);
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
		public void Transform_ACompletion_GhostIsAppended_AndPainted()
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
		public void Transform_TheGhostIsInsertedAtTheCaret_AndExposedForAcceptance()
		{
			_completion = new InputCompletionResult { Span = new InputCompletionSpan(0, 4), GhostText = "XYZ" };
			_caretIndex = 1;

			var provider = Provider(Grilling);
			var result = provider.Transform("abc");

			Assert.Equal("aXYZbc", result.RenderedText);
			Assert.Equal(new TextHighlightSpan(1, 3, SlashCommandHighlightPalette.Default.Ghost), Single(result));
			Assert.Equal("XYZ", provider.CompletionText);
			Assert.Equal(4, provider.TokenEnd);
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
