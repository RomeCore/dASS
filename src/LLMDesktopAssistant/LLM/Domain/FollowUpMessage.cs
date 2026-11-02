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
		private string _content = string.Empty;
		/// <summary>
		/// Gets or sets the text exactly as the user composed it, including any command token that never ran.
		/// </summary>
		public string Content
		{
			get => _content;
			set => SetProperty(ref _content, value);
		}

		private string _senderLogin = "user";
		/// <summary>
		/// Gets or sets the login of the participant that queued the message.
		/// </summary>
		public string SenderLogin
		{
			get => _senderLogin;
			set => SetProperty(ref _senderLogin, value);
		}

		private readonly AdditionalChatDataCollection _parts = [];
		/// <summary>
		/// The message parts attached when the message was queued, as copies of the draft's parts.
		/// </summary>
		public AdditionalChatDataCollection Parts
		{
			get => _parts;
			set => _parts.Reset(value);
		}

		private MessageVisibility _visibility = MessageVisibility.Always;
		/// <summary>
		/// Gets or sets the visibility the message will be inserted with.
		/// </summary>
		public MessageVisibility Visibility
		{
			get => _visibility;
			set => SetProperty(ref _visibility, value);
		}

		private List<string> _visibleTo = [];
		/// <summary>
		/// Gets or sets the logins or agent ids the message is restricted to when the visibility requires it.
		/// </summary>
		public List<string> VisibleTo
		{
			get => _visibleTo;
			set => SetProperty(ref _visibleTo, value);
		}

		private bool _isVisibleToWhiteList;
		/// <summary>
		/// Gets or sets a value indicating whether <see cref="VisibleTo"/> is a whitelist filter.
		/// </summary>
		public bool IsVisibleToWhiteList
		{
			get => _isVisibleToWhiteList;
			set => SetProperty(ref _isVisibleToWhiteList, value);
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

		private bool _canExecuteCommand = true;
		/// <summary>
		/// Gets or sets a value indicating whether this message may run a slash command. When false the content is
		/// inserted verbatim: no token extraction, no execution and no escape removal.
		/// </summary>
		public bool CanExecuteCommand
		{
			get => _canExecuteCommand;
			set => SetProperty(ref _canExecuteCommand, value);
		}

		private DateTime _createdAt = DateTime.Now;
		/// <summary>
		/// Gets or sets the moment the message was queued, used for ordering the queue dialog.
		/// </summary>
		public DateTime CreatedAt
		{
			get => _createdAt;
			set => SetProperty(ref _createdAt, value);
		}

		/// <summary>
		/// Projects the entry back onto the input of the send pipeline, so a follow-up travels the same path as a
		/// freshly typed message.
		/// </summary>
		public UserInput ToUserInput() => new()
		{
			Content = Content,
			SenderLogin = SenderLogin,
			Parts = [.. Parts],
			Visibility = Visibility,
			VisibleTo = [.. VisibleTo],
			IsVisibleToWhiteList = IsVisibleToWhiteList,
			CanExecuteCommand = CanExecuteCommand,
		};
	}
}
