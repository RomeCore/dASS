using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// One row of the completion picker: a continuation plus the view state the popup draws — its icon and whether the
	/// row is the selected one. The popup is a plain stack rather than a list control, so the selection flag lives here.
	/// </summary>
	public sealed class InputCompletionRow : NotifyPropertyChanged
	{
		public InputCompletionRow(InputCompletionItem item)
		{
			Item = item ?? throw new ArgumentNullException(nameof(item));
		}

		/// <summary>The continuation the row offers.</summary>
		public InputCompletionItem Item { get; }

		/// <summary>
		/// The row's icon: the continuation's explicit <see cref="InputCompletionItem.IconOverride"/> when set, otherwise
		/// the one chosen by its kind.
		/// </summary>
		public VisualIconKind Icon => Item.IconOverride ?? InputCompletionIcons.For(Item.Kind);

		/// <summary>
		/// Whether this is the row <see cref="InputCompletionViewModel.Accept"/> would accept.
		/// </summary>
		public bool IsSelected
		{
			get;
			set => SetProperty(ref field, value);
		}
	}
}
