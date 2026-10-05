using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// The stylesheet included in <c>App.axaml</c> in place of Material.Icons' <c>MaterialIconStyles</c>.
	/// </summary>
	public partial class VisualIconStyles : Styles
	{
		/// <summary>Initializes a new instance of the <see cref="VisualIconStyles"/> class.</summary>
		public VisualIconStyles()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
