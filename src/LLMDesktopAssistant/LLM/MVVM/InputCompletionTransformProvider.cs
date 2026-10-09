using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// What the chat input draws itself with: the regions its completion sources paint, plus the ghost of the current
	/// completion. It is the one object the input view binds to, so the view knows nothing about the features that
	/// colour it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The regions come from <see cref="IInputCompletionRenderer"/>, which a source implements next to
	/// <see cref="IInputCompletionSource"/> — what a feature completes and how it looks stay in one place.
	/// </para>
	/// <para>
	/// The ghost is a projection of the current <see cref="InputCompletionResult"/> rather than of the source that
	/// produced it, so closing the completion (Escape, a pointer-driven caret move) takes the preview away with it, and
	/// the preview can never outlive the completion it belongs to.
	/// </para>
	/// </remarks>
	[ChatService(typeof(IHighlightTransformProvider))]
	public sealed class InputCompletionTransformProvider : IHighlightTransformProvider, IInlineCompletionProvider
	{
		/// <summary>The ghost's colour when the theme provides none.</summary>
		private static readonly IBrush DefaultGhost = new SolidColorBrush(Color.Parse("#8C8C8C"));

		private readonly IInputCompletionService _completion;
		private readonly IReadOnlyList<IInputCompletionRenderer> _renderers;
		private readonly IBrush _ghost;

		public InputCompletionTransformProvider(IInputCompletionService completion,
			IEnumerable<IInputCompletionRenderer> renderers)
		{
			ArgumentNullException.ThrowIfNull(renderers);

			_completion = completion ?? throw new ArgumentNullException(nameof(completion));
			_renderers = [.. renderers];
			_ghost = ResolveGhost();

			// A new completion — or a closed one — changes what the preview shows, so the layout has to be redone.
			_completion.ResultChanged += (_, _) => NotifyLayoutChanged();
		}

		/// <inheritdoc/>
		public event EventHandler? LayoutChanged;

		/// <inheritdoc/>
		public string? CompletionText { get; private set; }

		/// <inheritdoc/>
		public int TokenEnd { get; private set; }

		/// <summary>Notifies the control that the visual output changed without a text change.</summary>
		public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

		/// <inheritdoc/>
		public HighlightTransformResult Transform(string text, int caretIndex)
		{
			text ??= string.Empty;
			caretIndex = Math.Clamp(caretIndex, 0, text.Length);
			CompletionText = null;
			TokenEnd = 0;

			var spans = new List<TextHighlightSpan>();
			foreach (var renderer in _renderers)
			{
				if (renderer.Render(text, caretIndex)?.HighlightSpans is { } regionSpans)
					spans.AddRange(regionSpans);
			}

			string? rendered = null;
			if (_completion.Result is { GhostText: { Length: > 0 } ghost } completion)
			{
				// The ghost *replaces* the tail of the region the completion would replace rather than being inserted
				// before it: previewing the missing part of a token ("wait=tru" → "e") would otherwise draw the real tail
				// a second time ("truee").
				var tailEnd = Math.Clamp(completion.Span.End, caretIndex, text.Length);

				CompletionText = ghost;
				TokenEnd = completion.Span.End;

				rendered = text[..caretIndex] + ghost + text[tailEnd..];
				spans.Add(new TextHighlightSpan(caretIndex, ghost.Length, _ghost));
			}

			return new HighlightTransformResult(rendered, spans.Count > 0 ? spans : null);
		}

		private static IBrush ResolveGhost()
		{
			if (Application.Current is not { } app)
				return DefaultGhost;

			return app.TryFindResource("InputCompletionGhostBrush", out var value) && value is IBrush brush
				? brush
				: DefaultGhost;
		}
	}
}
