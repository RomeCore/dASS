using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM;

/// <summary>
/// View for a single <see cref="AgentTaskViewModel"/> - displays a compact task card
/// with status icon, name, summary, and recursive sub-tasks.
/// </summary>
public partial class AgentTaskView : UserControl
{
	/// <summary>
	/// Converter that maps <see cref="bool"/> to the CSS class name "pulse" when <see langword="true"/>,
	/// which triggers a subtle pulsing animation on the status icon.
	/// </summary>
	public static readonly IValueConverter IsRunningToPulseClassConverter = new FuncValueConverter<bool, string?>(isRunning =>
		isRunning ? "pulse" : null);

	public AgentTaskView()
	{
		InitializeComponent();
	}
}
