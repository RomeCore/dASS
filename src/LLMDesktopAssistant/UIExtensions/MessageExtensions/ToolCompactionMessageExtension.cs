using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Adds a button to toggle tool results compaction on the message.
	/// Compactable tool results of the upper messages are replaced with placeholders for agents.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class ToolCompactionMessageExtension : MessageExtension
	{
		public override int Order => 52;

		public ToolCompactionMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.ArchiveOutline;

			Tooltip = "message.toggle_tool_compaction";

			Command = new RelayCommand(() =>
				MessageExtensionHelpers.ToggleCheckpoint(viewModel, ContextCheckpointKind.ToolCompaction));
		}
	}
}
