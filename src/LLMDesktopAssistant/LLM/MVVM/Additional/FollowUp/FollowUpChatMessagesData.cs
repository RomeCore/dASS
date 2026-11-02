using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.FollowUp
{
	/// <summary>
	/// The chat-level additional-data item that holds every pending follow-up of one chat, in composition order,
	/// together with the level the next entry is composed with.
	/// <para/>
	/// State, not a rendered message part: the item is attached to the chat itself and is invisible in the message
	/// UI. It is created lazily on the first enqueue, so a chat without pending messages carries no item at all.
	/// </summary>
	public class FollowUpChatMessagesData : AdditionalChatData
	{
		/// <summary>
		/// Creates the item in its hidden state: it carries state for the input, it is never rendered as a part.
		/// </summary>
		public FollowUpChatMessagesData()
		{
			IsVisible = false;
		}

		private readonly RangeObservableCollection<FollowUpMessage> _messages = [];
		/// <summary>
		/// The pending follow-ups, in the order they were composed.
		/// </summary>
		public RangeObservableCollection<FollowUpMessage> Messages
		{
			get => _messages;
			set => _messages.Reset(value);
		}

		private FollowUpInjectionLevel _defaultLevel = FollowUpInjectionLevel.Message;
		/// <summary>
		/// Gets or sets the level the next enqueued message gets: the last value the user picked.
		/// </summary>
		public FollowUpInjectionLevel DefaultLevel
		{
			get => _defaultLevel;
			set => SetProperty(ref _defaultLevel, value);
		}
	}
}
