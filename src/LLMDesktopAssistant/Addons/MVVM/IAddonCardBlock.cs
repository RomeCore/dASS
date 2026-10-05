using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <summary>
	/// A block of the addon card: an optional title, an arbitrary content and optional chips below it.
	/// The <see cref="Visibility"/> determines whether the block is inline, collapsible or a details section part.
	/// </summary>
	public interface IAddonCardBlock : IAddonCardElement
	{
		/// <summary>
		/// The title that will be shown on top of the content of this block.
		/// </summary>
		LocaleKeyBase? Title { get; }

		/// <summary>
		/// Where and when this block is rendered. Assigning
		/// <see cref="AddonCardBlockVisibility.Collapsible"/> collapses the block on creation
		/// (see <see cref="IsExpanded"/>).
		/// </summary>
		AddonCardBlockVisibility Visibility { get; }

		/// <summary>
		/// The content that will be shown inside this block.
		/// </summary>
		object? Content { get; }

		/// <summary>
		/// The chips that will be shown below the content of this block.
		/// </summary>
		ImmutableList<IAddonCardChip>? Chips { get; }

		/// <summary>
		/// The icon of the toggle button that expands this block.
		/// Only used by <see cref="AddonCardBlockVisibility.Collapsible"/> blocks.
		/// </summary>
		VisualIconKind? ToggleIcon { get; }

		/// <summary>
		/// The tooltip of the toggle button that expands this block.
		/// Only used by <see cref="AddonCardBlockVisibility.Collapsible"/> blocks.
		/// </summary>
		LocaleKeyBase? ToggleToolTip { get; }

		/// <summary>
		/// Whether the block is currently shown inside its slot: the common block template gates the
		/// rendering of every block on this value. Defaults to <see langword="true"/>
		/// (<see cref="AddonCardBlockVisibility.Collapsible"/> blocks are collapsed on creation) and is
		/// toggled by the block's own button in the card action row afterwards.
		/// </summary>
		bool IsExpanded { get; set; }
	}
}
