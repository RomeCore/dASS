using Avalonia.Media.Imaging;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.UIExtensions.MessageExtensions;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageView))]
	public class AssistantMessageViewModel : MessageViewModelBase
	{
		private readonly AssistantMessage _assistantMessage;
		public AssistantMessage AssistantMessage => _assistantMessage;

		public bool ShowAvatar { get; }
		public Bitmap? SenderAvatar { get; }
		public string? SenderName { get; }

		private bool _isCompleted;
		public bool IsCompleted
		{
			get => _isCompleted;
			private set => SetProperty(ref _isCompleted, value);
		}

		/// <summary>
		/// The reasoning part of the message. Always created; visibility is managed internally.
		/// </summary>
		public AssistantMessageReasoningPartViewModel ReasoningPart { get; }

		/// <summary>
		/// The textual part of the message. Always created; visibility is managed internally.
		/// </summary>
		public AssistantMessageTextPartViewModel TextPart { get; }

		public ImmutableList<MessageExtension> Extensions { get; }

		protected override bool DefaultRenderMarkdown => true;

		public AssistantMessageViewModel(BranchedMessage branchedMessage, ChatViewModel chatVM) : base(branchedMessage, chatVM)
		{
			if (branchedMessage.Message is not AssistantMessage assistantMessage)
				throw new InvalidOperationException("Invalid message type. Expected AssistantMessage.");
			_assistantMessage = assistantMessage;

			// Determine that we can apply avatar
			var prevMessage = branchedMessage.MessageIndex - 1 >= 0
				? chatVM.Chat.Messages[branchedMessage.MessageIndex - 1].Message as AssistantMessage
				: null;
			if (prevMessage == null || assistantMessage.SenderAgentId != prevMessage.SenderAgentId)
			{
				var agentManager = chatVM.Chat.Services.GetRequiredService<IAgentManagementService>();
				var agent = agentManager.GetAgentDescriptor(assistantMessage.SenderAgentId);

				try
				{
					if (!string.IsNullOrWhiteSpace(agent.Info.Base64ProfileImage))
					{
						var bytes = Convert.FromBase64String(agent.Info.Base64ProfileImage);
						using var ms = new MemoryStream(bytes);
						SenderAvatar = new Bitmap(ms);
					}
				}
				catch
				{
					SenderAvatar = null;
				}
				SenderName = agent.Info.Name;
				ShowAvatar = true;
			}

			ReasoningPart = new AssistantMessageReasoningPartViewModel(assistantMessage) { RenderMarkdown = RenderMarkdown };
			TextPart = new AssistantMessageTextPartViewModel(assistantMessage) { RenderMarkdown = RenderMarkdown };
			Extensions = MessageExtensionManager.CreateExtensions(this, chatVM.Chat);

			SubscribeToAssistantMessageEvents();
		}

		protected override void OnRenderMarkdownChanged()
		{
			ReasoningPart.RenderMarkdown = RenderMarkdown;
			TextPart.RenderMarkdown = RenderMarkdown;
		}

		private void SubscribeToAssistantMessageEvents()
		{
			IsCompleted = _assistantMessage.IsCompleted;
			if (_assistantMessage.IsCompleted) return;

			_assistantMessage.CompletionToken.OnCompleted(() => IsCompleted = true);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ReasoningPart.Dispose();
				TextPart.Dispose();

				foreach (var extension in Extensions)
					extension.Dispose();
			}
		}
	}
}
