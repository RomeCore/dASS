using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	public class MessageViewModelBase : ViewModelBase
	{
		private readonly BranchedMessage branchedMessage;
		private readonly IChatOperationService chatOperator;

		/// <summary>
		/// Gets the current message being displayed.
		/// </summary>
		public ChatMessage Message => branchedMessage.Message;

		private LocaleKeyBase? _error;
		/// <summary>
		/// Gets the error message associated with the message, if any. Shared by every message type.
		/// </summary>
		public LocaleKeyBase? Error
		{
			get => _error;
			private set => SetProperty(ref _error, value);
		}

		public ICommand RegenerateCommand { get; }
		public ICommand ResendCommand { get; }
		public ICommand DeleteCommand { get; }
		public ICommand SwitchBranchCommand { get; }
		public ICommand ToggleRenderMarkdownCommand { get; }

		public IEnumerable<int> BranchIndices =>
			Enumerable.Range(1, branchedMessage.AvailableBranchesCount);

		public int SelectedBranchIndex
		{
			get => branchedMessage.SelectedBranchIndex + 1;
			set => chatOperator.SwitchBranch(branchedMessage.MessageIndex, value - 1);
		}
		public int AvailableBranchesCount => branchedMessage.AvailableBranchesCount;
		public int PreviousBranchIndex => SelectedBranchIndex - 1;
		public int NextBranchIndex => SelectedBranchIndex + 1;
		public bool BranchSelectionAvailable => branchedMessage.AvailableBranchesCount > 1;

		public ChatViewModel ChatViewModel { get; }

		/// <summary>
		/// The tool-call list part of the message. Always created; visibility is managed internally.
		/// </summary>
		public ToolCallListViewModel ToolCalls { get; }

		/// <summary>
		/// The additional data of the message (chips + everything else). Always created.
		/// </summary>
		public AdditionalChatDataCollectionViewModel AdditionalData { get; }

		/// <summary>
		/// Gets the default markdown rendering mode of the message, overridden by the message type.
		/// </summary>
		protected virtual bool DefaultRenderMarkdown => true;

		private bool _renderMarkdown;
		/// <summary>
		/// Gets or sets a value indicating whether the textual parts of the message are rendered as Markdown.
		/// This is a runtime-only preference (not persisted).
		/// </summary>
		public bool RenderMarkdown
		{
			get => _renderMarkdown;
			set
			{
				if (SetProperty(ref _renderMarkdown, value))
					OnRenderMarkdownChanged();
			}
		}

		private bool _isDisabledForAgents;
		/// <summary>
		/// Gets or sets a value indicating whether the message is hidden from every agent. The state is kept on the
		/// message itself, so it is persisted and shared by every view of the chat.
		/// </summary>
		public bool IsDisabledForAgents
		{
			get => _isDisabledForAgents;
			set
			{
				if (SetProperty(ref _isDisabledForAgents, value))
				{
					Message.IsDisabledForAgents = value;
					RaisePropertyChanged(nameof(ContentOpacity));
				}
			}
		}

		/// <summary>
		/// Gets the opacity of the whole message: a message hidden from agents is dimmed.
		/// </summary>
		public double ContentOpacity => IsDisabledForAgents ? 0.75 : 1.0;

		/// <summary>
		/// Gets a value indicating whether the message contains tool calls.
		/// </summary>
		public bool ContainsToolCalls => ToolCalls.HasToolCalls;

		public MessageViewModelBase(BranchedMessage branchedMessage, ChatViewModel chatVM)
		{
			this.branchedMessage = branchedMessage;
			ChatViewModel = chatVM;
			chatOperator = chatVM.Chat.Services.GetRequiredService<IChatOperationService>();

			ToolCalls = new ToolCallListViewModel(branchedMessage.Message, chatVM.Chat);
			AdditionalData = new AdditionalChatDataCollectionViewModel(branchedMessage.Message.AdditionalData);

			Error = Message.Error;
			Message.PropertyChanged += OnMessagePropertyChanged;

			_renderMarkdown = DefaultRenderMarkdown;
			_isDisabledForAgents = Message.IsDisabledForAgents;

			ToolCalls.PropertyChanged += OnToolCallsPropertyChanged;

			RegenerateCommand = new RelayCommand(() =>
			{
				chatOperator.RegenerateMessageAsync(branchedMessage.MessageIndex);
			});

			ResendCommand = new RelayCommand(() =>
			{
				chatOperator.ResendMessageAsync(branchedMessage.MessageIndex);
			});

			DeleteCommand = new RelayCommand(() =>
			{
				chatOperator.DeleteMessageWithDescendants(branchedMessage.MessageIndex);
			});

			SwitchBranchCommand = new RelayCommand<int>(branchIndex =>
			{
				chatOperator.SwitchBranch(branchedMessage.MessageIndex, branchIndex - 1);
			},
				branchIndex => branchIndex - 1 >= 0 && branchIndex - 1 < branchedMessage.AvailableBranchesCount);

			ToggleRenderMarkdownCommand = new RelayCommand(() => RenderMarkdown = !RenderMarkdown);
		}

		private void OnToolCallsPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ToolCallListViewModel.HasToolCalls))
				RaisePropertyChanged(nameof(ContainsToolCalls));
		}

		private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ChatMessage.Error))
				InvokeUI(() => Error = Message.Error);
			else if (e.PropertyName == nameof(ChatMessage.IsDisabledForAgents))
				InvokeUI(() =>
				{
					_isDisabledForAgents = Message.IsDisabledForAgents;
					RaisePropertyChanged(nameof(IsDisabledForAgents));
					RaisePropertyChanged(nameof(ContentOpacity));
				});
		}

		/// <summary>
		/// Called when <see cref="RenderMarkdown"/> changes; used to propagate the mode to the textual parts.
		/// </summary>
		protected virtual void OnRenderMarkdownChanged()
		{
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Message.PropertyChanged -= OnMessagePropertyChanged;
				ToolCalls.PropertyChanged -= OnToolCallsPropertyChanged;
				ToolCalls.Dispose();
				AdditionalData.Dispose();
			}
		}
	}
}
