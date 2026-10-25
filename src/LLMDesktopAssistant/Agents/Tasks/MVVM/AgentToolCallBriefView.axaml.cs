using Avalonia.Controls;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM;

/// <summary>
/// View for <see cref="AgentToolCallBriefViewModel"/> - renders a compact tool call card
/// with status, arguments preview, behaviour flags, and confirmation buttons.
/// </summary>
[ViewFor(typeof(AgentToolCallBriefViewModel))]
public partial class AgentToolCallBriefView : UserControl
{
	public AgentToolCallBriefView()
	{
		InitializeComponent();
	}
}
