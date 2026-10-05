using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <summary>
	/// A compact labeled element of the addon card (badge, tag, source indicator, diagnostic flag).
	/// The same element covers three shapes: icon only, label only, and icon + label.
	/// </summary>
	public interface IAddonCardChip : IAddonCardElement
	{
		/// <summary>
		/// Whether the chip has a border.
		/// </summary>
		bool HasBorder { get; }

		/// <summary>
		/// The brush of the border and the icon. If null, the default accent color will be used.
		/// </summary>
		IBrush? Brush { get; }

		/// <summary>
		/// The opacity of the whole chip. Useful for muted elements, such as the source indicator.
		/// </summary>
		double Opacity { get; }

		/// <summary>
		/// The icon to display on the chip. If null, no icon will be displayed.
		/// </summary>
		VisualIconKind? Icon { get; }

		/// <summary>
		/// The label to display on the chip after the icon. If null, no label will be displayed.
		/// </summary>
		LocaleKeyBase? Label { get; }

		/// <summary>
		/// The tooltip to display when the user hovers over the chip. If null, no tooltip will be displayed.
		/// </summary>
		LocaleKeyBase? ToolTip { get; }

		/// <summary>
		/// An arbitrary content shown instead of (or before) the icon and the label.
		/// Used for compound chips, such as a memory block with its attachment mode.
		/// </summary>
		object? Content { get; }
	}
}
