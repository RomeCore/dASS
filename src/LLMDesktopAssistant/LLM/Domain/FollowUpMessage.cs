using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// One pending follow-up: a message the user composed while the chat was busy and that has not reached the
	/// history yet. Persisted inside
	/// <see cref="MVVM.Additional.FollowUp.FollowUpChatMessagesData"/>, which owns the whole queue of one chat.
	/// </summary>
	public class FollowUpMessage : NotifyPropertyChanged
	{
		private DateTime _createdAt = DateTime.Now;
		/// <summary>
		/// Gets or sets the moment the message was queued, used for ordering the queue dialog.
		/// </summary>
		public DateTime CreatedAt
		{
			get => _createdAt;
			set => SetProperty(ref _createdAt, value);
		}

		private FollowUpInjectionLevel _level = FollowUpInjectionLevel.Message;
		/// <summary>
		/// Gets or sets the earliest barrier this entry may be injected at. The queue's gate is the minimum of these.
		/// </summary>
		public FollowUpInjectionLevel Level
		{
			get => _level;
			set => SetProperty(ref _level, value);
		}

		private bool _generateIntent = true;
		/// <summary>
		/// Gets or sets a value indicating whether this message asked for an answer. Any entry asking for one makes
		/// the whole drained batch generate once.
		/// </summary>
		public bool GenerateIntent
		{
			get => _generateIntent;
			set => SetProperty(ref _generateIntent, value);
		}

		private UserInputState _userInput = new();
		/// <summary>
		/// Gets or sets the user input that contains the content, visibility, sender and other data to create the user message.
		/// </summary>
		public UserInputState UserInput
		{
			get => _userInput;
			set => SetProperty(ref _userInput, value);
		}
	}
}
