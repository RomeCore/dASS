using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.LLM.Services;
using Material.Icons;
using Serilog;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Adds a button to summarize the message with all previous messages.
	/// If a summary already exists, removes it.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class SummarizeMessageExtension : MessageExtension
	{
		public override int Order => 51;

		public SummarizeMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.TextBoxSearchOutline;

			Tooltip = "message.summarize_message";

			Command = new AsyncRelayCommand(async () =>
			{
				try
				{
					var existing = viewModel.Message.AdditionalData.TryGet<ContextCheckpoint>();
					if (existing != null && existing.Kind.HasFlag(ContextCheckpointKind.Summary))
					{
						MessageExtensionHelpers.ToggleCheckpoint(viewModel, ContextCheckpointKind.Summary);
						return;
					}

					var summarizer = viewModel.ChatViewModel.Chat.Services.GetRequiredService<IChatSummarizationService>();
					await summarizer.SummarizeMessageWithPreviousMessagesAsync(viewModel.Message);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to summarize message: {Error}", ex);
				}
			});
		}
	}
}
