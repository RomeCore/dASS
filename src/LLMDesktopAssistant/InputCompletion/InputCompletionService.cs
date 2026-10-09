using System;
using System.Collections.Generic;
using System.Linq;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <inheritdoc cref="IInputCompletionService"/>
	/// <remarks>
	/// Sources are ordered by <see cref="IInputCompletionSource.Priority"/> descending once, at construction; the first
	/// one that claims the caret wins and the rest are not asked. The result is kept rather than recomputed on demand,
	/// so the popup, the renderer of the input and the input's own accept all work off the same completion.
	/// </remarks>
	[ChatService(typeof(IInputCompletionService))]
	public class InputCompletionService : IInputCompletionService
	{
		private readonly IReadOnlyList<IInputCompletionSource> _sources;

		public IHighlightTransformProvider CompletionTransformProvider { get; }

		public InputCompletionService(IEnumerable<IInputCompletionSource> sources)
		{
			ArgumentNullException.ThrowIfNull(sources);

			_sources = sources
				.OrderByDescending(source => source.Priority)
				.ThenBy(source => source.GetType().FullName, StringComparer.Ordinal)
				.ToList();

			CompletionTransformProvider = new InputCompletionTransformProvider(this, _sources);
		}

		/// <inheritdoc/>
		public event EventHandler? ResultChanged;

		/// <inheritdoc/>
		public event EventHandler? SelectedIndexChanged;

		/// <inheritdoc/>
		public InputCompletionResult? Result { get; private set; }

		/// <inheritdoc/>
		public int SelectedIndex { get; private set; }

		/// <inheritdoc/>
		public void Update(string? text, int caretIndex)
		{
			SetResult(Compute(text ?? string.Empty, caretIndex));
		}

		/// <inheritdoc/>
		public void Select(int completionIndex)
		{
			SelectedIndex = completionIndex;
			SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc/>
		public void Close()
		{
			SetResult(null);
		}

		private InputCompletionResult? Compute(string text, int caretIndex)
		{
			foreach (var source in _sources)
				if (source.TryCompute(text, caretIndex) is { } result)
					return result;

			return null;
		}

		private void SetResult(InputCompletionResult? result)
		{
			if (ReferenceEquals(Result, result))
				return;

			Result = result;
			SelectedIndex = 0;
			ResultChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
