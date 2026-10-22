using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Registers the shared, context-free processors: the ones that make sense for any selection, on any surface.
	/// </summary>
	[Service(typeof(IMarkdownSelectionProcessorsProvider))]
	public sealed class DefaultSelectionProcessorsProvider : IMarkdownSelectionProcessorsProvider
	{
		/// <inheritdoc />
		public MarkdownSelectionProcessorScope Scope => MarkdownSelectionProcessorScope.Default;

		/// <inheritdoc />
		public IReadOnlyList<IMarkdownSelectionProcessor> GetProcessors(MarkdownSelectionContext context)
			=> [CopySelectionProcessor.Instance];
	}
}
