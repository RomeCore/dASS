using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	public class AddonCardAction : AddonCardElementBase, IAddonCardAction
	{
		/// <inheritdoc/>
		public required VisualIconKind Icon { get; init; }

		/// <inheritdoc/>
		public required ICommand Command { get; init; }

		/// <inheritdoc/>
		public object? CommandParameter { get; init; }

		/// <inheritdoc/>
		public LocaleKeyBase? ToolTip { get; init; }
	}
}
