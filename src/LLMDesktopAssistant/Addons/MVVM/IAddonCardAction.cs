using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	public interface IAddonCardAction : IAddonCardElement
	{
		/// <summary>
		/// Icon to be displayed on the action button.
		/// </summary>
		VisualIconKind Icon { get; }

		/// <summary>
		/// Command that will be executed when the action button is clicked.
		/// </summary>
		ICommand Command { get; }

		/// <summary>
		/// The parameter passed to <see cref="Command"/>.
		/// </summary>
		object? CommandParameter { get; }

		/// <summary>
		/// Tooltip that will be shown on hover over the action button.
		/// </summary>
		LocaleKeyBase? ToolTip { get; }
	}
}
