using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

		private AssistantMessageReasoningPartViewModel? _reasoningPart;
		public AssistantMessageReasoningPartViewModel? ReasoningPart
		{
			get => _reasoningPart;
			private set => SetProperty(ref _reasoningPart, value);
		}

		private AssistantMessageTextPartViewModel? _textPart;
		public AssistantMessageTextPartViewModel? TextPart
		{
			get => _textPart;
			private set => SetProperty(ref _textPart, value);
		}

		private AssistantMessageToolPartViewModel? _toolPart;
		public AssistantMessageToolPartViewModel? ToolPart
		{
			get => _toolPart;
			private set => SetProperty(ref _toolPart, value);
		}

		private string? _error;
		public string? Error
		{
			get => _error;
			set => SetProperty(ref _error, value);
		}

		public ImmutableList<MessageExtension> Extensions { get; }

		public bool ContainsToolCalls => _assistantMessage.ToolCalls.Count > 0;

		public AssistantMessageViewModel(BranchedMessage branchedMessage, ChatViewModel chatVM) : base(branchedMessage, chatVM)
		{
			if (branchedMessage.Message is not AssistantMessage assistantMessage)
				throw new InvalidOperationException("Invalid message type. Expected IAssistantMessage.");
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

			if (!string.IsNullOrEmpty(assistantMessage.ReasoningContent))
			{
				ReasoningPart ??= new AssistantMessageReasoningPartViewModel(assistantMessage);
			}
			if (!string.IsNullOrEmpty(assistantMessage.Content))
			{
				TextPart ??= new AssistantMessageTextPartViewModel(assistantMessage);
			}
			if (assistantMessage.ToolCalls.Count > 0)
			{
				ToolPart ??= new AssistantMessageToolPartViewModel
				{
					ToolCalls = new ObservableCollection<ToolCallViewModel>(
						assistantMessage.ToolCalls.Select(t => new ToolCallViewModel(t, chatVM.Chat)))
				};
			}
			Error = assistantMessage.Error;
			Extensions = MessageExtensionManager.CreateExtensions(this, chatVM.Chat);

			SubscribeToAssistantMessageEvents();
		}

		private void SubscribeToAssistantMessageEvents()
		{
			IsCompleted = _assistantMessage.IsCompleted;
			if (_assistantMessage.IsCompleted) return;

			void OnMessagePropertyChanged(object? s, PropertyChangedEventArgs e)
			{
				InvokeUI(() =>
				{
					switch (e.PropertyName)
					{
						case nameof(AssistantMessage.ReasoningContent):

							if (ReasoningPart == null && !string.IsNullOrEmpty(_assistantMessage.ReasoningContent))
							{
								ReasoningPart = new AssistantMessageReasoningPartViewModel(_assistantMessage);
							}
							break;

						case nameof(AssistantMessage.Content):

							if (TextPart == null && !string.IsNullOrEmpty(_assistantMessage.Content))
							{
								TextPart = new AssistantMessageTextPartViewModel(_assistantMessage);
							}
							break;
					}


					Error = _assistantMessage.Error;
				});
			}

			void OnToolCallsChanged(object? sender, NotifyCollectionChangedEventArgs e)
			{
				if (e.NewItems != null)
				{
					InvokeUI(() =>
					{
						ToolPart ??= new AssistantMessageToolPartViewModel();
						foreach (ToolCall toolCall in e.NewItems)
						{
							ToolPart.ToolCalls.Add(new ToolCallViewModel(toolCall, ChatViewModel.Chat));
						}

						RaisePropertyChanged(nameof(ContainsToolCalls));
					});
				}
			}

			_assistantMessage.PropertyChanged += OnMessagePropertyChanged;
			_assistantMessage.ToolCalls.CollectionChanged += OnToolCallsChanged;

			_assistantMessage.CompletionToken.OnCompleted(() =>
			{
				_assistantMessage.PropertyChanged -= OnMessagePropertyChanged;
				_assistantMessage.ToolCalls.CollectionChanged -= OnToolCallsChanged;
				IsCompleted = true;
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ReasoningPart?.Dispose();
				ReasoningPart = null;
				TextPart?.Dispose();
				TextPart = null;
				ToolPart?.Dispose();
				ToolPart = null;

				foreach (var extension in Extensions)
					extension.Dispose();
			}
		}
	}
}