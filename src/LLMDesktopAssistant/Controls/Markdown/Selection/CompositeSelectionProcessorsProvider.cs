namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Concatenates several providers into the single provider a <see cref="MarkdownControl"/> accepts, so a
	/// consumer can merge the container-provided set with its own per-surface actions without touching the control.
	/// </summary>
	public sealed class CompositeSelectionProcessorsProvider : IMarkdownSelectionProcessorsProvider
	{
		private readonly IReadOnlyList<IMarkdownSelectionProcessorsProvider> _providers;

		/// <summary>
		/// Creates the composite, dropping <see langword="null"/> entries (so an optional provider can be passed
		/// inline).
		/// </summary>
		public CompositeSelectionProcessorsProvider(params IMarkdownSelectionProcessorsProvider?[] providers)
			=> _providers = [.. providers.OfType<IMarkdownSelectionProcessorsProvider>()];

		/// <summary>
		/// Creates the composite from an arbitrary sequence of providers.
		/// </summary>
		public CompositeSelectionProcessorsProvider(IEnumerable<IMarkdownSelectionProcessorsProvider> providers)
			=> _providers = [.. providers];

		
		/// <inheritdoc />
		public IReadOnlyList<IMarkdownSelectionProcessor> GetProcessors(MarkdownSelectionContext context)
			=> [.. _providers.SelectMany(provider => provider.GetProcessors(context))];
	}
}
