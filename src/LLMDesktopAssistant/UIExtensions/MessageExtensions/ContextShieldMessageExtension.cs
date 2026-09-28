using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Adds a button to toggle the context shield on the message.
	/// The context shield prevents further messages from being included into the context.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class ContextShieldMessageExtension : MessageExtension
	{
		public override int Order => 50;

		public ContextShieldMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.ShieldOutline;

			Tooltip = "message.toggle_context_shield";

			Command = new RelayCommand(() =>
				MessageExtensionHelpers.ToggleCheckpoint(viewModel, ContextCheckpointKind.Shield));
		}
	}
}
