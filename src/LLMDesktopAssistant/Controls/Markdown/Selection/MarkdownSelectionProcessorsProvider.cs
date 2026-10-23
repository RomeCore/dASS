namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// The simplest provider: a fixed list of processors built once by the consumer.
	/// </summary>
	public sealed class MarkdownSelectionProcessorsProvider(params IMarkdownSelectionProcessor[] processors)
		: IMarkdownSelectionProcessorsProvider
	{
		
		/// <inheritdoc />
		public IReadOnlyList<IMarkdownSelectionProcessor> GetProcessors(MarkdownSelectionContext context) => processors;
	}
}
