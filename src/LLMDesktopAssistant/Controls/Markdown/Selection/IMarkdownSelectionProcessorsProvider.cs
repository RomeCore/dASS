namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Supplies the actions the selection toolbar of one <see cref="MarkdownControl"/> offers.
	/// </summary>
	/// <remarks>
	/// The provider is handed to the control by its consumer (a nullable property), never resolved from the
	/// container by the control itself: the control knows nothing about chats, messages or services, and the
	/// consumer owns the lifetime and the composition of the actions.
	/// </remarks>
	public interface IMarkdownSelectionProcessorsProvider
	{
		/// <summary>
		/// Gets the surfaces this provider serves. A provider is usually registered as a service next to the
		/// others, and the consumer composes the ones matching its surface - see
		/// <see cref="MarkdownSelectionProcessorProviders.ForScope"/>.
		/// </summary>
		MarkdownSelectionProcessorScope Scope => MarkdownSelectionProcessorScope.Default;

		/// <summary>
		/// Gets the processors of this provider for one selection, in their insertion order. The context lets a
		/// provider decide before the toolbar exists ("only inside a code block", "this chat has no quote book")
		/// instead of building buttons that are then dropped; the toolbar still filters them by
		/// <see cref="IMarkdownSelectionProcessor.IsApplicable"/> and sorts by
		/// <see cref="IMarkdownSelectionProcessor.Order"/>.
		/// </summary>
		IReadOnlyList<IMarkdownSelectionProcessor> GetProcessors(MarkdownSelectionContext context);
	}
}
