using System.ComponentModel;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// A single action button of a code block: a title, an icon, an optional panel under the block, and the work
	/// to run.
	/// </summary>
	/// <remarks>
	/// Unlike a selection processor, an action is a stateful object: it outlives the click and mutates itself while
	/// it runs (a spinner, a "stop" command, a result panel), which is why it raises
	/// <see cref="INotifyPropertyChanged"/> instead of being rebuilt per state.
	/// </remarks>
	public interface IMarkdownCodeBlockAction : INotifyPropertyChanged, IDisposable
	{
		/// <summary>
		/// Gets the localized button tooltip/title.
		/// </summary>
		LocaleKeyBase Title { get; }

		/// <summary>
		/// Gets the sort order inside the block's action row (ascending).
		/// </summary>
		int Order => 0;

		/// <summary>
		/// Gets a value indicating whether the button is offered for the current block.
		/// </summary>
		bool IsVisible { get; }

		/// <summary>
		/// Gets the icon shown on the button.
		/// </summary>
		VisualIconKind Icon { get; }

		/// <summary>
		/// Gets the content appended under the code block: an Avalonia control or a view model resolved by the
		/// view locator. <see langword="null"/> means the action contributes no panel.
		/// </summary>
		object? AdditionalContent { get; }

		/// <summary>
		/// Runs the action. The context is built at click time, so it carries the current content, not the one
		/// the action was created with.
		/// </summary>
		Task ExecuteAsync(MarkdownCodeBlockContext context, CancellationToken cancellationToken);
	}
}
