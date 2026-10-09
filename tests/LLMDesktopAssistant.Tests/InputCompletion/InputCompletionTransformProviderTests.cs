using System;
using System.Collections.Generic;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.MVVM;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The input's renderer: the regions its completion sources paint, plus the ghost of the current completion — which
	/// is a projection of that completion, so closing it takes the preview away.
	/// </summary>
	public class InputCompletionTransformProviderTests
	{
		private sealed class FakeService : IInputCompletionService
		{
			public event EventHandler? ResultChanged;

			public InputCompletionResult? Result { get; private set; }

			public void Update(string? text, int caretIndex) => Set(Result);

			public void Close() => Set(null);

			public void Set(InputCompletionResult? result)
			{
				Result = result;
				ResultChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private sealed class FakeRenderer(HighlightTransformResult? result) : IInputCompletionRenderer
		{
			public string? LastText { get; private set; }
			public int LastCaret { get; private set; }

			public HighlightTransformResult? Render(string text, int caretIndex)
			{
				LastText = text;
				LastCaret = caretIndex;
				return result;
			}
		}

		private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

		[Fact]
		public void Transform_WithNothingToDraw_ReturnsNothing()
		{
			var service = new FakeService();
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("hello", 5);

			Assert.Null(result.RenderedText);
			Assert.Null(result.HighlightSpans);
			Assert.Null(provider.CompletionText);
			Assert.Equal(0, provider.TokenEnd);
		}

		[Fact]
		public void Transform_ConcatenatesTheRegionsOfEveryRenderer()
		{
			var service = new FakeService();
			var first = new FakeRenderer(new HighlightTransformResult(
				[new TextHighlightSpan(0, 3, Brush("#FF0000"))]));
			var second = new FakeRenderer(new HighlightTransformResult(
				[new TextHighlightSpan(4, 2, Brush("#00FF00"))]));
			var provider = new InputCompletionTransformProvider(service, [first, second]);

			var result = provider.Transform("/gr do", 6);

			Assert.Equal(2, result.HighlightSpans!.Count);
			Assert.Equal(0, result.HighlightSpans[0].Start);
			Assert.Equal(4, result.HighlightSpans[1].Start);
		}

		[Fact]
		public void Transform_HandsTheTextAndCaretToTheRenderers()
		{
			var service = new FakeService();
			var renderer = new FakeRenderer(null);
			var provider = new InputCompletionTransformProvider(service, [renderer]);

			provider.Transform("/gr", 3);

			Assert.Equal("/gr", renderer.LastText);
			Assert.Equal(3, renderer.LastCaret);
		}

		[Fact]
		public void Transform_ReplacesTheCompletionTailWithTheGhost_AndExposesItForAcceptance()
		{
			var service = new FakeService();
			service.Set(new InputCompletionResult
			{
				Span = new InputCompletionSpan(11, 2),
				GhostText = "rue"
			});
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("agent wait=tr wait=false", 12);

			Assert.Equal("agent wait=true wait=false", result.RenderedText);
			Assert.Equal("rue", provider.CompletionText);
			Assert.Equal(13, provider.TokenEnd);
		}

		[Fact]
		public void Transform_WithAClosedCompletion_HasNoGhost()
		{
			var service = new FakeService();
			service.Set(new InputCompletionResult
			{
				Span = new InputCompletionSpan(0, 3),
				GhostText = "illing"
			});
			var provider = new InputCompletionTransformProvider(service, []);
			Assert.Equal("/grilling", provider.Transform("/gr", 3).RenderedText);

			service.Close();

			var result = provider.Transform("/gr", 3);

			Assert.Null(result.RenderedText);
			Assert.Null(provider.CompletionText);
		}

		[Fact]
		public void Transform_WithAGhostAtTheRegionEnd_DrawsTheGhostPastTheText()
		{
			// "abc" with the caret at its end: nothing to replace, the ghost is appended — the same shape as the
			// "wait=tru" → "ue" preview, only with an empty tail.
			var service = new FakeService();
			service.Set(new InputCompletionResult
			{
				Span = new InputCompletionSpan(0, 3),
				GhostText = "def"
			});
			var provider = new InputCompletionTransformProvider(service, []);

			var result = provider.Transform("abc", 3);

			Assert.Equal("abcdef", result.RenderedText);
			Assert.Equal(3, provider.TokenEnd);
		}

		[Fact]
		public void ResultChanged_NotifiesTheControlToRedoTheLayout()
		{
			var service = new FakeService();
			var provider = new InputCompletionTransformProvider(service, []);
			var raised = 0;
			provider.LayoutChanged += (_, _) => raised++;

			service.Set(new InputCompletionResult { Span = new InputCompletionSpan(0, 1) });

			Assert.Equal(1, raised);
		}
	}
}
