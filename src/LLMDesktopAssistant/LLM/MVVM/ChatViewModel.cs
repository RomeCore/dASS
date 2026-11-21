using System.ComponentModel;
using System.Windows.Input;
using LLMDesktopAssistant.Agents.Tasks.MVVM;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM
{
	[ViewModelFor(typeof(ChatView))]
	public class ChatViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the message sequence that represents the conversation history.
		/// </summary>
		public MessageSequenceViewModel MessageSequence { get; }

		/// <summary>
		/// Gets the chat status view model.
		/// </summary>
		public ChatStatusViewModel ChatStatus { get; }

		/// <summary>
		/// Gets the conversation manager that manages the current conversation.
		/// </summary>
		public Chat Chat { get; }

		/// <summary>
		/// Gets the bottom panel of the chat (user input + toolbars).
		/// </summary>
		public ChatBottomPanelViewModel ChatBottomPanel { get; }

		/// <summary>
		/// Gets the fallback side panel over the chat's own additional data.
		/// </summary>
		public ChatSidePanelViewModel ChatSidePanel { get; }

		/// <summary>
		/// Gets the overlay layer over the message sequence, with the tray its windows are restored from.
		/// </summary>
		public ChatOverlayHostViewModel ChatOverlay { get; }

		/// <summary>
		/// Gets the tab strip of the header and the payload of the open tab.
		/// </summary>
		public ChatTabHostViewModel ChatTabs { get; }

		/// <summary>
		/// Gets or sets the command that shows or hides the chat list this chat belongs to.
		/// </summary>
		/// <remarks>
		/// The list belongs to the manager that hosts the chat pages, not to the chat, so the command arrives from
		/// there; a chat shown without a list leaves it <see langword="null"/> and the header hides the button.
		/// </remarks>
		public ICommand? ToggleChatListCommand { get; set; }

		/// <summary>
		/// Gets the title the header shows.
		/// </summary>
		public string Title => Chat.Title;

		/// <summary>
		/// Gets a value indicating whether the chat has a title of its own.
		/// </summary>
		public bool HasTitle => !string.IsNullOrWhiteSpace(Chat.Title);

		/// <summary>
		/// Gets a value indicating whether the chat has a topic to show under the title.
		/// </summary>
		public bool HasTopic => !string.IsNullOrWhiteSpace(Chat.Topic);

		/// <summary>
		/// Gets a value indicating whether the chat has a sub-topic to show next to the topic.
		/// </summary>
		public bool HasSubTopic => !string.IsNullOrWhiteSpace(Chat.SubTopic);

		/// <summary>
		/// Gets the color the topic is drawn with, derived from its own hash.
		/// </summary>
		public string TopicColor => ChatTopicHashColor.GetHex(Chat.Topic);

		/// <summary>
		/// Gets the color the sub-topic is drawn with, derived from its own hash.
		/// </summary>
		public string SubTopicColor => ChatTopicHashColor.GetHex(Chat.SubTopic);

		private AgentTaskListViewModel? _agentTaskList;
		/// <summary>
		/// Gets the view model for the list of agent tasks associated with this chat.
		/// </summary>
		public AgentTaskListViewModel? AgentTaskList
		{
			get => _agentTaskList;
			private set => SetProperty(ref _agentTaskList, value);
		}

		private int _runningTaskCount;
		/// <summary>
		/// The number of currently running agent tasks in this chat.
		/// </summary>
		public int RunningTaskCount
		{
			get => _runningTaskCount;
			private set => SetProperty(ref _runningTaskCount, value);
		}

		private bool _hasRunningTasks;
		/// <summary>
		/// Whether this chat has any running agent tasks.
		/// </summary>
		public bool HasRunningTasks
		{
			get => _hasRunningTasks;
			private set => SetProperty(ref _hasRunningTasks, value);
		}

		private VisualIconKind _agentTaskIcon = MaterialIconKind.TimerSand;
		/// <summary>
		/// The icon for the agent tasks button, reflecting whether tasks are running.
		/// </summary>
		public VisualIconKind AgentTaskIcon
		{
			get => _agentTaskIcon;
			private set => SetProperty(ref _agentTaskIcon, value);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatViewModel"/> class.
		/// </summary>
		public ChatViewModel(Chat chat)
		{
			Chat = chat;
			ChatSidePanel = new ChatSidePanelViewModel(chat.AdditionalData);
			ChatOverlay = new ChatOverlayHostViewModel(chat, chat.AdditionalData);
			ChatTabs = new ChatTabHostViewModel(chat, chat.AdditionalData);
			chat.PropertyChanged += OnChatPropertyChanged;
			ChatBottomPanel = new ChatBottomPanelViewModel(this);
			ChatStatus = new ChatStatusViewModel(chat);
			MessageSequence = new MessageSequenceViewModel(this, ChatBottomPanel.UserInput);

			AgentTaskList = new AgentTaskListViewModel
			{
				Filtering = AgentTaskListFiltering.Chat
			};
			AgentTaskList.PropertyChanged += OnAgentTaskListPropertyChanged;
			AgentTaskList.BindToSource(chat.AgentTasks);
		}

		private void OnAgentTaskListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (AgentTaskList == null) return;

			switch (e.PropertyName)
			{
				case nameof(AgentTaskListViewModel.RunningTaskCount):
					RunningTaskCount = AgentTaskList.RunningTaskCount;
					break;

				case nameof(AgentTaskListViewModel.HasRunningTasks):
					HasRunningTasks = AgentTaskList.HasRunningTasks;
					AgentTaskIcon = AgentTaskList.HasRunningTasks
						? MaterialIconKind.TimerSandComplete
						: MaterialIconKind.TimerSand;
					break;
			}
		}

		/// <summary>
		/// The header reads the chat's own identity, so its changes are re-raised as this view model's.
		/// </summary>
		private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not (null or nameof(Chat.Title) or nameof(Chat.Topic) or nameof(Chat.SubTopic)))
				return;

			InvokeUI(() =>
			{
				RaisePropertyChanged(nameof(Title));
				RaisePropertyChanged(nameof(HasTitle));
				RaisePropertyChanged(nameof(HasTopic));
				RaisePropertyChanged(nameof(HasSubTopic));
				RaisePropertyChanged(nameof(TopicColor));
				RaisePropertyChanged(nameof(SubTopicColor));
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				if (AgentTaskList != null)
					AgentTaskList.PropertyChanged -= OnAgentTaskListPropertyChanged;

				Chat.PropertyChanged -= OnChatPropertyChanged;
				MessageSequence.Dispose();
				ChatSidePanel.Dispose();
				ChatOverlay.Dispose();
				ChatTabs.Dispose();
				ChatBottomPanel.Dispose();
				ChatStatus.Dispose();
				AgentTaskList?.Dispose();
			}
		}
	}
}
