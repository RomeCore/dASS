namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Supplies preview tabs for fenced code blocks - a rendered diagram, a page, a formula, a nested markdown.
	/// </summary>
	/// <remarks>
	/// One provider contributes at most one tab per block, and the tab is created once per block: the provider is
	/// asked as soon as the block exists, and answers <see langword="null"/> for every language it does not serve.
	/// The returned preview is then updated by the block itself on every content change.
	/// </remarks>
	public interface ICodeBlockPreviewProvider : IMarkdownExtensionProvider
	{
		/// <summary>
		/// Gets the position of the provider's tab among the tabs of a block (ascending). The provider with the
		/// lowest order, that also returns <see langword="true"/> from <see cref="ICodeBlockPreview.IsDefault"/>,
		/// provides the tab the block opens on.
		/// </summary>
		int Order => 0;

		/// <summary>
		/// Creates the preview for one block, or returns <see langword="null"/> when this provider does not serve
		/// the block's language.
		/// </summary>
		/// <remarks>
		/// Must be cheap to answer "no": read <see cref="MarkdownCodeBlockContext.Content"/> first and build the
		/// control only after the block is claimed.
		/// </remarks>
		ICodeBlockPreview? TryCreatePreview(MarkdownCodeBlockContext context);
	}
}
