using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LLMDesktopAssistant.Desktop.Scripting.PowerShell;

/// <summary>
/// The PowerShell block of the chat environment settings.
/// </summary>
public partial class PowerShellEnvironmentConfigurationView : UserControl
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PowerShellEnvironmentConfigurationView"/> class.
	/// </summary>
	public PowerShellEnvironmentConfigurationView()
	{
		InitializeComponent();
	}

	private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
