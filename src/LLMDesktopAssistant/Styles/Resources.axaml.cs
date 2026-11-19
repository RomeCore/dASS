using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LLMDesktopAssistant.Styles;

public partial class Resources : ResourceDictionary
{
	public Resources()
	{
		AvaloniaXamlLoader.Load(this);
	}
}