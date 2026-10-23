namespace LLMDesktopAssistant.Controls.Markdown
{
	/// <summary>
	/// The common part of every provider that extends a markdown surface: the surfaces it serves.
	/// </summary>
	/// <remarks>
	/// Providers are ordinary services (<see cref="Microsoft.Extensions.DependencyInjection"/>
	/// <c>IEnumerable&lt;TProvider&gt;</c>), and a consumer composes the ones matching its own surface with
	/// <see cref="MarkdownExtensionProviders.Resolve{TProvider}"/> instead of carrying its own registry.
	/// </remarks>
	public interface IMarkdownExtensionProvider
	{
		/// <summary>
		/// Gets the surfaces this provider serves. <see cref="MarkdownExtensionScope.Default"/> means everywhere.
		/// </summary>
		MarkdownExtensionScope Scope => MarkdownExtensionScope.Default;
	}
}
