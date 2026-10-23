using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Controls.Markdown
{
	/// <summary>
	/// Composes the markdown extension providers a surface needs out of the ones registered in the container.
	/// </summary>
	public static class MarkdownExtensionProviders
	{
		/// <summary>
		/// Resolves every registered provider of <typeparamref name="TProvider"/> that serves
		/// <paramref name="scope"/>, plus every provider whose scope is
		/// <see cref="MarkdownExtensionScope.Default"/>.
		/// </summary>
		/// <remarks>
		/// There is no registry service on purpose: an ordinary
		/// <c>IEnumerable&lt;TProvider&gt;</c> registration already hands out every provider, and which surface
		/// a provider serves is read straight from its <see cref="IMarkdownExtensionProvider.Scope"/>.
		/// </remarks>
		public static IReadOnlyList<TProvider> Resolve<TProvider>(IServiceProvider services, MarkdownExtensionScope scope)
			where TProvider : class, IMarkdownExtensionProvider
			=> [.. services.GetServices<TProvider>()
				.Where(provider => provider.Scope == MarkdownExtensionScope.Default
					|| (provider.Scope & scope) != 0)];
	}
}
