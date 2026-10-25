using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LLMDesktopAssistant.Desktop.Scripting.Bash;

/// <summary>
/// The bash block of the chat environment settings.
/// </summary>
public partial class BashEnvironmentConfigurationView : UserControl
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BashEnvironmentConfigurationView"/> class.
	/// </summary>
	public BashEnvironmentConfigurationView()
	{
		InitializeComponent();
	}

	private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
