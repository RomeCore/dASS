using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents a chat session.
	/// </summary>
	public class Chat(IServiceProvider services) : ChatObjectBase
	{
		/// <summary>
		/// Gets the service provider used to resolve dependencies.
		/// </summary>
		public IServiceProvider Services { get; } = services;

		/// <summary>
		/// The ID of the chat.
		/// </summary>
		public int Id { get; internal set; }

		private string _topic = string.Empty;
		/// <summary>
		/// Gets or sets the topic/category of the chat session.
		/// This is a human-readable category like "coding", "roleplay", "dnd", etc.
		/// The color for this topic is generated from its hash for consistent UI display.
		/// </summary>
		public string Topic
		{
			get => _topic;
			set => SetProperty(ref _topic, value);
		}

		private string _title = string.Empty;
		/// <summary>
		/// Gets or sets the title of the chat session.
		/// </summary>
		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		/// <summary>
		/// The collection of messages in the chat session.
		/// These are managed by <see cref="IChatStorageService"/>.
		/// </summary>
		public RangeObservableCollection<BranchedMessage> Messages { get; } = [];

		private UserInputState _userInputState = new();
		/// <summary>
		/// Gets or sets the persisted input state of the chat (draft text and parts).
		/// Managed by <see cref="IChatStorageService"/>.
		/// </summary>
		public UserInputState UserInputState
		{
			get => _userInputState;
			set => SetProperty(ref _userInputState, value);
		}

		/// <summary>
		/// Gets the collection of agent tasks associated with this chat session.
		/// </summary>
		public RangeObservableCollection<AgentTask> AgentTasks { get; } = [];

		/// <summary>
		/// Gets or sets the list of tool modules that are available for use in the chat session.
		/// </summary>
		public List<ToolModule> AdditionalTools { get; set; } = [];



		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var message in Messages)
				{
					message.Dispose();
				}
			}
		}
	}
}