using Avalonia.Controls;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM;

/// <summary>
/// View for <see cref="AgentTaskDispatcherViewModel"/> - the global agent task dispatcher
/// shown as a sidebar tab. Includes a search field, filters, and the full task list.
/// </summary>
public partial class AgentTaskDispatcherView : UserControl
{
	public AgentTaskDispatcherView()
	{
		InitializeComponent();
	}
}
