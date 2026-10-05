using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM.Grouping
{
	/// <summary>
	/// A way the cards of an addon list are grouped: 'no grouping' (a flat list), 'by category',
	/// 'by addon pack', and anything else a host provides. A mode is a stateless strategy: it maps
	/// an addon to the key of the group the addon belongs to.
	/// </summary>
	/// <remarks>
	/// The non-generic base exists for the compiled bindings of the list panel: the mode selector
	/// browses and displays the modes without knowing the addon type.
	/// </remarks>
	public abstract class GroupingMode
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GroupingMode"/> class.
		/// </summary>
		/// <param name="title">The display name of the mode, shown in the mode selector.</param>
		/// <param name="icon">The icon of the mode, shown in the mode selector.</param>
		protected GroupingMode(LocaleKeyBase title, VisualIconKind? icon = null)
		{
			Title = title;
			Icon = icon;
		}

		/// <summary>
		/// Gets the display name of the mode, shown in the mode selector.
		/// </summary>
		public LocaleKeyBase Title { get; }

		/// <summary>
		/// Gets the icon of the mode, shown in the mode selector.
		/// </summary>
		public VisualIconKind? Icon { get; }
	}

	/// <summary>
	/// The typed grouping mode: knows how to map an addon of the list to its group.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addon of the list.</typeparam>
	public abstract class GroupingMode<TAddon> : GroupingMode
		where TAddon : AddonBase<TAddon>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GroupingMode{TAddon}"/> class.
		/// </summary>
		/// <param name="title">The display name of the mode, shown in the mode selector.</param>
		/// <param name="icon">The icon of the mode, shown in the mode selector.</param>
		protected GroupingMode(LocaleKeyBase title, VisualIconKind? icon = null)
			: base(title, icon)
		{
		}

		/// <summary>
		/// Gets the group the addon belongs to, or <see langword="null"/> when the mode does not group
		/// the addon (its card is shown on the top level of the list).
		/// </summary>
		public abstract AddonGroupKey? GetGroupKey(TAddon addon);
	}
}
