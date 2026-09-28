using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Adds a button to toggle reasoning compaction on the message.
	/// Reasoning content of the upper messages is removed entirely for agents.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class ReasoningCompactionMessageExtension : MessageExtension
	{
		public override int Order => 54;

		public ReasoningCompactionMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.Brain;

			Tooltip = "message.toggle_reasoning_compaction";

			Command = new RelayCommand(() =>
				MessageExtensionHelpers.ToggleCheckpoint(viewModel, ContextCheckpointKind.ReasoningCompaction));
		}
	}
}
