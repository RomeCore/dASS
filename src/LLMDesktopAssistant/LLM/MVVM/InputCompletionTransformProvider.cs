using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// What the chat input draws itself with: the regions its completion sources paint, plus the ghost of the current
	/// completion. It is the one object the input view binds to, so the view knows nothing about the features that
	/// colour it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The regions come from <see cref="IInputCompletionSource"/>, which a source implements next to its completion —
	/// what a feature completes and how it looks stay in one place.
	/// </para>
	/// <para>
	/// The ghost is a projection of the current <see cref="InputCompletionResult"/> — the selected continuation — rather
	/// than of the source that produced it, so closing the completion (Escape, a pointer-driven caret move) takes the
	/// preview away with it, and the preview can never outlive the completion it belongs to.
	/// </para>
	/// </remarks>
	public sealed class InputCompletionTransformProvider : IHighlightTransformProvider
	{
		/// <summary>
		/// The ghost's colour when the theme provides none.
		/// </summary>
		private static readonly IBrush DefaultGhost = new SolidColorBrush(Color.Parse("#8C8C8C"));

		private readonly IInputCompletionService _completion;
		private readonly IReadOnlyList<IInputCompletionSource> _renderers;
		private readonly IBrush _ghost;

		public InputCompletionTransformProvider(IInputCompletionService completion,
			IEnumerable<IInputCompletionSource> renderers)
		{
			ArgumentNullException.ThrowIfNull(renderers);

			_completion = completion ?? throw new ArgumentNullException(nameof(completion));
			_renderers = [.. renderers];
			_ghost = ResolveGhost();

			// A new completion — or a closed one — changes what the preview shows, so the layout has to be redone.
			_completion.ResultChanged += (_, _) => NotifyLayoutChanged();

			// So does the selection: the ghost previews the *selected* continuation, not the first one.
			_completion.SelectedIndexChanged += (_, _) => NotifyLayoutChanged();
		}

		/// <inheritdoc/>
		public event EventHandler? LayoutChanged;

		/// <summary>
		/// Notifies the control that the visual output changed without a text change.
		/// </summary>
		public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

		/// <inheritdoc/>
		public HighlightTransformResult Transform(string text, int caretIndex)
		{
			text ??= string.Empty;
			caretIndex = Math.Clamp(caretIndex, 0, text.Length);

			var spans = new List<TextHighlightSpan>();
			foreach (var renderer in _renderers)
				if (renderer.TryHighlight(text, caretIndex) is { } regionSpans)
				{
					spans.AddRange(regionSpans);
					break;
				}

			string? rendered = null;
			if (_completion.Result is { } completion
				&& completion.ItemAt(_completion.SelectedIndex) is { } item
				&& completion.GhostOf(item) is { Length: > 0 } ghost)
			{
				// The ghost *replaces* the tail of the region the completion would replace rather than being inserted
				// before it: previewing the missing part of a token ("wait=tru" → "e") would otherwise draw the real tail
				// a second time ("truee"). It is drawn at the result's own caret, so the preview and the accept agree.
				var caret = Math.Clamp(completion.CaretIndex, 0, text.Length);
				var tailEnd = Math.Clamp(completion.Span.End, caret, text.Length);
				var delta = ghost.Length - (tailEnd - caret); // how much the text after the insertion shifts

				// If the ghost is shorter than the text it replaces, it is padded with spaces to match the length of the
				// replacement.
				if (delta < 0)
				{
					ghost += new string(' ', -delta);
					delta = 0;
				}

				rendered = text[..caret] + ghost + text[tailEnd..];

				// The renderers paint the *raw* text; the ghost overwrites [caret, tailEnd). Remap every region onto the
				// rendered text — trim the ones the ghost swallowed, shift the ones behind it — so that no region bleeds
				// out of the ghost and the spans stay sorted and non-overlapping (TextLayout mispaints otherwise).
				for (var i = 0; i < spans.Count; i++)
				{
					var span = spans[i];
					if (span.Start >= tailEnd)
						spans[i] = span with { Start = span.Start + delta };
					else if (span.Start + span.Length > caret)
						spans[i] = span with { Length = caret - Math.Min(span.Start, caret) };
				}

				spans.Add(new TextHighlightSpan(caret, ghost.Length, _ghost));
				spans.Sort((a, b) => a.Start.CompareTo(b.Start));
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
