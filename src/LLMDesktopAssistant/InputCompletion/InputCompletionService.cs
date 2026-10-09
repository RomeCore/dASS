using System.Collections.Generic;
using System.Linq;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.InputCompletion
{
	/// <inheritdoc cref="IInputCompletionService"/>
	/// <remarks>
	/// Sources are ordered by <see cref="IInputCompletionSource.Priority"/> descending once, at construction; the
	/// first one that claims the caret wins and the rest are not asked.
	/// </remarks>
	[ChatService(typeof(IInputCompletionService))]
	public class InputCompletionService : IInputCompletionService
	{
		private readonly IReadOnlyList<IInputCompletionSource> _sources;

		public InputCompletionService(IEnumerable<IInputCompletionSource> sources)
		{
			_sources = sources
				.OrderByDescending(source => source.Priority)
				.ThenBy(source => source.GetType().FullName, System.StringComparer.Ordinal)
				.ToList();
		}

		/// <inheritdoc/>
		public InputCompletionResult? Compute(InputCompletionRequest request)
		{
			foreach (var source in _sources)
			{
				if (source.TryCompute(request, out var result))
					return result;
			}

			return null;
		}
	}
}
