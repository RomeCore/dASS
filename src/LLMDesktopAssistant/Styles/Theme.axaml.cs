using Avalonia.Markup.Xaml;

namespace LLMDesktopAssistant.Styles;

public partial class Theme : Avalonia.Styling.Styles
{
	public Theme()
	{
		AvaloniaXamlLoader.Load(this);
	}
}