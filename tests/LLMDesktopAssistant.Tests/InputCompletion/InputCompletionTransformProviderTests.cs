using System;
using System.Collections.Generic;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.MVVM;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The input's renderer: the regions the first claiming completion source paints, plus the ghost of the current
	/// completion's selected continuation — a projection of that completion, so closing it takes the preview away.
	/// </summary>
	public class InputCompletionTransformProviderTests
	{
		private sealed class FakeService : IInputCompletionService
		{
			public event EventHandler? ResultChanged;
			public event EventHandler? SelectedIndexChanged;

			public InputCompletionResult? Result { get; private set; }
			public int SelectedIndex { get; private set; }
			public IHighlightTransformProvider CompletionTransformProvider => null!;

			public void Update(string? text, int caretIndex) { }

			public void Select(int completionIndex)
			{
				SelectedIndex = completionIndex;
				SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
			}

			public void Close() => Set(null);

			public void Set(InputCompletionResult? result)
			{
				Result = result;
				SelectedIndex = 0;
				ResultChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private sealed class FakeSource(IReadOnlyList<TextHighlightSpan>? spans) : IInputCompletionSource
		{
			public int Priority => 0;
			public string? LastText { get; private set; }
			public int LastCaret { get; private set; }

			public InputCompletionResult? TryCompute(string text, int caretIndex) => null;

			public IReadOnlyList<TextHighlightSpan>? TryHighlight(string text, int caretIndex)
			{
				LastText = text;
				LastCaret = caretIndex;
				return spans;
			}
		}

		private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

		private static InputCompletionResult Ghost(string text, int caretIndex, InputCompletionSpan span, string insertText)
			=> new()
			{
				Text = text,
				CaretIndex = caretIndex,
				Span = span,
				Items = [new InputCompletionItem { InsertText = insertText }]
			};

		[Fact]
		public void Transform_WithNothingToDraw_ReturnsNothing()
		{
			var provider = new InputCompletionTransformProvider(new FakeService(), []);

			var result = provider.Transform("hello", 5);

			Assert.Null(result.RenderedText);
			Assert.Null(result.HighlightSpans);
		}

		[Fact]
		public void Transform_PaintsTheFirstClaimingSource()
		{
			var first = new FakeSource([new TextHighlightSpan(0, 3, Brush("#FF0000"))]);
			var second = new FakeSource([new TextHighlightSpan(4, 2, Brush("#00FF00"))]);
			var provider = new InputCompletionTransformProvider(new FakeService(), [first, second]);

			var result = provider.Transform("/gr do", 6);

			var span = Assert.Single(result.HighlightSpans!);
			Assert.Equal(0, span.Start);
		}

		[Fact]
		public void Transform_HandsTheTextAndCaretToTheSources()
		{
			var source = new FakeSource(null);
			var provider = new InputCompletionTransformProvider(new FakeService(), [source]);

			provider.Transform("/gr", 3);

			Assert.Equal("/gr", source.LastText);
			Assert.Equal(3, source.LastCaret);
		}

		[Fact]
		public void Transform_ReplacesTheCompletionTailWithTheGhost()
		{
			// "wait=tr" → the ghost previews the missing "ue" of "true", replacing the tail rather than doubling it.
			var service = new FakeService();
			service.Set(Ghost("agent wait=tr wait=false", 12, new InputCompletionSpan(11, 2), "true"));
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("agent wait=tr wait=false", 12);

			Assert.Equal("agent wait=true wait=false", result.RenderedText);
			var span = Assert.Single(result.HighlightSpans!);
			Assert.Equal(12, span.Start);
			Assert.Equal(3, span.Length);
		}

		[Fact]
		public void Transform_WithAClosedCompletion_HasNoGhost()
		{
			var service = new FakeService();
			service.Set(Ghost("/gr", 3, new InputCompletionSpan(0, 3), "/grilling"));
			var provider = new InputCompletionTransformProvider(service, []);
			Assert.Equal("/grilling", provider.Transform("/gr", 3).RenderedText);

			service.Close();

			var result = provider.Transform("/gr", 3);

			Assert.Null(result.RenderedText);
		}

		[Fact]
		public void Transform_WithAGhostAtTheRegionEnd_DrawsTheGhostPastTheText()
		{
			// "abc" with the caret at its end: nothing to replace, the ghost is appended.
			var service = new FakeService();
			service.Set(Ghost("abc", 3, new InputCompletionSpan(0, 3), "abcdef"));
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("abc", 3);

			Assert.Equal("abcdef", result.RenderedText);
		}

		[Fact]
		public void Transform_WithAnItemThatDoesNotContinueTheTypedText_HasNoGhost()
		{
			var service = new FakeService();
			service.Set(Ghost("/sk", 3, new InputCompletionSpan(0, 3), "/grilling"));
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("/sk", 3);

			Assert.Null(result.RenderedText);
		}

		[Fact]
		public void ResultChanged_NotifiesTheControlToRedoTheLayout()
		{
			var service = new FakeService();
			var provider = new InputCompletionTransformProvider(service, []);
			var raised = 0;
			provider.LayoutChanged += (_, _) => raised++;

			service.Set(Ghost("/gr", 3, new InputCompletionSpan(0, 3), "/grilling"));

			Assert.Equal(1, raised);
		}

		[Fact]
		public void SelectedIndexChanged_NotifiesTheControlToRedoTheLayout()
		{
			var service = new FakeService();
			var provider = new InputCompletionTransformProvider(service, []);
			var raised = 0;
			provider.LayoutChanged += (_, _) => raised++;

			service.Select(1);

			Assert.Equal(1, raised);
		}
	}
}
