using Avalonia.Controls;

namespace LLMDesktopAssistant.LLM.MVVM.Additional;

/// <summary>
/// View for <see cref="AgentTaskInlineViewModel"/> - delegates rendering to the embedded
/// <see cref="AgentTaskViewModel"/> via a <see cref="ContentControl"/>.
/// </summary>
[ViewFor(typeof(AgentTaskListInlineViewModel))]
public partial class AgentTaskListInlineView : UserControl
{
	public AgentTaskListInlineView()
	{
		InitializeComponent();
	}
}
