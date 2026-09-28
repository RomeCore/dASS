using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Adds a button to toggle forced tool results compaction on the message.
	/// All tool results of the upper messages are replaced with placeholders for agents,
	/// ignoring per-call compactibility.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class ForcedToolCompactionMessageExtension : MessageExtension
	{
		public override int Order => 53;

		public ForcedToolCompactionMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.Archive;

			Tooltip = "message.toggle_forced_tool_compaction";

			Command = new RelayCommand(() =>
				MessageExtensionHelpers.ToggleCheckpoint(viewModel, ContextCheckpointKind.ForcedToolCompaction));
		}
	}
}
