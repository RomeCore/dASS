using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM.Grouping
{
	/// <summary>
	/// The identity and the display data of one group of an addon list.
	/// </summary>
	public sealed class AddonGroupKey
	{
		/// <summary>
		/// Gets the stable identifier of the group, used as the grouping key of the list
		/// (for example <c>category:tool.category.filesystem</c> or <c>pack:C:\...\agents</c>).
		/// </summary>
		public required string Id { get; init; }

		/// <summary>
		/// Gets the display name of the group (the title of the group card).
		/// </summary>
		public required LocaleKeyBase Title { get; init; }

		/// <summary>
		/// Gets the icon of the group.
		/// </summary>
		public VisualIconKind? Icon { get; init; }

		/// <summary>
		/// Gets the brush of the group name prefix, if any.
		/// </summary>
		public IBrush? Brush { get; init; }
	}
}
