using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <inheritdoc cref="IAddonCardChip"/>
	public class AddonCardChip : AddonCardElementBase, IAddonCardChip
	{
		/// <inheritdoc/>
		public bool HasBorder { get; init; } = true;

		/// <inheritdoc/>
		public IBrush? Brush { get; init; }

		/// <inheritdoc/>
		public double Opacity { get; init; } = 1;

		/// <inheritdoc/>
		public VisualIconKind? Icon { get; init; }

		/// <inheritdoc/>
		public LocaleKeyBase? Label { get; init; }

		/// <inheritdoc/>
		public LocaleKeyBase? ToolTip { get; init; }

		/// <inheritdoc/>
		public object? Content { get; init; }
	}
}
