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
		/// Gets the user input to be sent in the next conversation turn.
		/// </summary>
		public UserInputViewModel UserInput { get; }

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

		private MaterialIconKind _agentTaskIcon = MaterialIconKind.TimerSand;
		/// <summary>
		/// The icon for the agent tasks button, reflecting whether tasks are running.
		/// </summary>
		public MaterialIconKind AgentTaskIcon
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
			UserInput = new UserInputViewModel(this);
			ChatStatus = new ChatStatusViewModel(chat);
			MessageSequence = new MessageSequenceViewModel(this);

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

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				if (AgentTaskList != null)
					AgentTaskList.PropertyChanged -= OnAgentTaskListPropertyChanged;

				MessageSequence.Dispose();
				UserInput.Dispose();
				ChatStatus.Dispose();
				AgentTaskList?.Dispose();
			}
		}
	}
}