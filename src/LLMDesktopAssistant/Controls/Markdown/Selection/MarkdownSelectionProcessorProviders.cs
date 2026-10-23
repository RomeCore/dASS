namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Composes the selection processor providers a surface needs out of the ones registered in the container.
	/// </summary>
	public static class MarkdownSelectionProcessorProviders
	{
		/// <summary>
		/// Resolves the provider for one surface: every registered provider that serves
		/// <paramref name="scope"/>, plus every provider whose scope is
		/// <see cref="MarkdownExtensionScope.Default"/>.
		/// </summary>
		public static IMarkdownSelectionProcessorsProvider Resolve(
			IServiceProvider services,
			MarkdownExtensionScope scope)
			=> new CompositeSelectionProcessorsProvider(
				MarkdownExtensionProviders.Resolve<IMarkdownSelectionProcessorsProvider>(services, scope));
	}
}
