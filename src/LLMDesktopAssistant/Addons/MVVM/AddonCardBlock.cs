using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <inheritdoc cref="IAddonCardBlock"/>
	public class AddonCardBlock : AddonCardElementBase, IAddonCardBlock
	{
		/// <inheritdoc/>
		public LocaleKeyBase? Title { get; init; }

		/// <summary>
		/// Where and when this block is rendered. Assigning
		/// <see cref="AddonCardBlockVisibility.Collapsible"/> collapses the block: <see cref="IsExpanded"/>
		/// is set to <see langword="false"/> automatically, so collapsible blocks always start collapsed.
		/// If a block must start expanded, assign <see cref="IsExpanded"/> after this property.
		/// </summary>
		public AddonCardBlockVisibility Visibility
		{
			get;
			init
			{
				field = value;

				if (value == AddonCardBlockVisibility.Collapsible)
					IsExpanded = false;
			}
		}

		/// <inheritdoc/>
		public object? Content { get; init; }

		/// <inheritdoc/>
		public ImmutableList<IAddonCardChip>? Chips { get; init; }

		/// <inheritdoc/>
		public VisualIconKind? ToggleIcon { get; init; }

		/// <inheritdoc/>
		public LocaleKeyBase? ToggleToolTip { get; init; }

		/// <summary>
		/// Whether the block is currently shown in its slot: the common block template gates rendering of
		/// every block on this value. Defaults to <see langword="true"/>
		/// (<see cref="AddonCardBlockVisibility.Collapsible"/> blocks are collapsed on creation,
		/// see <see cref="Visibility"/>) and is toggled by the block's own button afterwards.
		/// </summary>
		public virtual bool IsExpanded
		{
			get;
			set => SetProperty(ref field, value);
		} = true;
	}
}
