using Avalonia.Controls;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM;

/// <summary>
/// View for <see cref="AgentTaskListViewModel"/> - renders a scrollable vertical list of agent tasks.
/// </summary>
public partial class AgentTaskListView : UserControl
{
	public AgentTaskListView()
	{
		InitializeComponent();
	}
}
