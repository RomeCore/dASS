using Microsoft.Extensions.DependencyInjection;

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
		/// <see cref="MarkdownSelectionProcessorScope.Default"/>.
		/// </summary>
		/// <remarks>
		/// There is no registry service on purpose: an ordinary
		/// <c>IEnumerable&lt;IMarkdownSelectionProcessorsProvider&gt;</c> registration already hands out every
		/// provider, and which surface a provider serves is read straight from its
		/// <see cref="IMarkdownSelectionProcessorsProvider.Scope"/>.
		/// </remarks>
		public static IMarkdownSelectionProcessorsProvider Resolve(IServiceProvider services,
			MarkdownSelectionProcessorScope scope)
			=> new CompositeSelectionProcessorsProvider(
				services.GetServices<IMarkdownSelectionProcessorsProvider>()
					.Where(provider => provider.Scope == MarkdownSelectionProcessorScope.Default
						|| (provider.Scope & scope) != 0));
	}
}
