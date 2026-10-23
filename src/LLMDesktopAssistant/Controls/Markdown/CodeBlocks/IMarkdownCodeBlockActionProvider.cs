namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// Supplies the action buttons of fenced code blocks: copy, save, run, and whatever comes next.
	/// </summary>
	/// <remarks>
	/// Replaces the reflection-scanned <c>CodeBlockExtension</c> types: a provider decides whether it serves a
	/// block before anything is created, so a block never instantiates actions that only get hidden afterwards.
	/// </remarks>
	public interface IMarkdownCodeBlockActionProvider : IMarkdownExtensionProvider
	{
		/// <summary>
		/// Gets the actions of this provider for one block, in their insertion order.
		/// </summary>
		/// <remarks>
		/// Must be cheap and synchronous: read <see cref="MarkdownCodeBlockContext.Content"/> first and build an
		/// action only after the block is claimed. The block owns every action it gets here and disposes them when
		/// it goes away.
		/// </remarks>
		IReadOnlyList<IMarkdownCodeBlockAction> GetActions(MarkdownCodeBlockContext context);
	}
}
