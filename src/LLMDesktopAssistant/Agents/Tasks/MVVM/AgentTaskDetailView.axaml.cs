using Avalonia.Controls;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM;

/// <summary>
/// View for the <see cref="AgentTaskDetailViewModel"/> - a dialog window
/// that displays an expanded, detailed view of an agent task with all its
/// messages, tool calls, statistics, sub-tasks, and error information.
/// </summary>
public partial class AgentTaskDetailView : UserControl
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AgentTaskDetailView"/> class.
	/// </summary>
	public AgentTaskDetailView()
	{
		InitializeComponent();
	}
}
