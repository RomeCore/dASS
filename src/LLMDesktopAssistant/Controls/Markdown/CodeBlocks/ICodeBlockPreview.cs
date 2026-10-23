using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// One preview tab of a code block: a title, the content the tab shows, and the incremental update that keeps
	/// it in sync with the source while the message streams.
	/// </summary>
	/// <remarks>
	/// A preview is created once per block and lives as long as the tab, so its content control is created once
	/// too - re-creating it on every change would throw away the scroll, zoom and layout state on each chunk.
	/// </remarks>
	public interface ICodeBlockPreview : IDisposable
	{
		/// <summary>
		/// Gets the localized tab title.
		/// </summary>
		LocaleKeyBase Title { get; }

		/// <summary>
		/// Gets a value indicating whether this preview is the tab the block opens on when nobody has chosen one.
		/// </summary>
		bool IsDefault => false;

		/// <summary>
		/// Gets a value indicating whether the block opens in split view (the source next to this preview) by
		/// default. Only read when <see cref="IsDefault"/> is <see langword="true"/>.
		/// </summary>
		bool IsShownSplitByDefault => false;

		/// <summary>
		/// Gets the content shown by the tab: an Avalonia control or a view model resolved by the view locator.
		/// </summary>
		/// <remarks>
		/// The getter must stay cheap and return the same instance on every call: the block may touch it more
		/// than once, and a re-created control loses the state above. Defer heavy initialisation (a web view, an
		/// engine) to the moment the control is attached to the visual tree.
		/// </remarks>
		object Content { get; }

		/// <summary>
		/// Updates the preview from the source: the whole block content, whether the block is final, and a token
		/// that is cancelled as soon as a newer update arrives.
		/// </summary>
		/// <remarks>
		/// Calls are serialized by the block owner (a newer update cancels the previous one), so the implementation
		/// does not have to guard against itself. <paramref name="isFinal"/> is <see langword="true"/> once the
		/// fence is closed or the message has stopped streaming - use it to render errors and to stop debouncing.
		/// </remarks>
		ValueTask UpdateAsync(CodeBlockContent content, bool isFinal, CancellationToken cancellationToken);
	}
}
