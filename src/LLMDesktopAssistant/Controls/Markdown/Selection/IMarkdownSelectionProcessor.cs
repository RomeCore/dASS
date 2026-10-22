using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// A single action the selection toolbar offers for the text the user selected in a
	/// <see cref="MarkdownRenderer"/>: a title, an icon, and the work to run.
	/// </summary>
	/// <remarks>
	/// A processor is a <em>descriptor</em>: the toolbar owns the look of the button, a processor owns only what
	/// the button says and what it does. That keeps one visual language across every action and keeps a processor
	/// author (including a script-backed one later) away from Avalonia's visual tree.
	/// </remarks>
	public interface IMarkdownSelectionProcessor
	{
		/// <summary>
		/// Gets the localized button tooltip/title.
		/// </summary>
		LocaleKeyBase Title { get; }

		/// <summary>
		/// Gets the icon shown on the button.
		/// </summary>
		VisualIconKind Icon { get; }

		/// <summary>
		/// Gets the sort order inside the toolbar (ascending). Providers are concatenated before sorting,
		/// so equal orders keep the provider order.
		/// </summary>
		int Order => 0;

		/// <summary>
		/// Gets a value indicating whether the processor is offered for a specific selection. The toolbar asks
		/// once, when it is about to be shown, and hides the button when this returns <see langword="false"/>.
		/// </summary>
		bool IsApplicable(MarkdownSelectionContext context) => true;

		/// <summary>
		/// Runs the action. The toolbar is already closed and the selection is already cleared when this is
		/// called, so the processor must read everything it needs from <paramref name="context"/>.
		/// </summary>
		Task ExecuteAsync(MarkdownSelectionContext context, CancellationToken cancellationToken = default);
	}
}
