using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents the user's input used for generating a response from the LLM assistant.
	/// </summary>
	public class UserInput
	{
		/// <summary>
		/// Gets or sets the content of the user input. This is typically the text entered by the user in a chat interface.
		/// </summary>
		public required string Content { get; init; }

		/// <summary>
		/// Gets or sets the login name of the sender who provided the user input.
		/// </summary>
		public required string SenderLogin { get; init; }

		/// <summary>
		/// Gets or sets the collection of message parts associated with the user input.
		/// These can include attached files, images or any other additional view models.
		/// If no parts are present, this property is an empty collection.
		/// </summary>
		public ImmutableList<AdditionalChatData> Parts { get; init; } = [];

		/// <summary>
		/// Gets or sets the visibility of the user input. Determines who can see the message.
		/// </summary>
		public MessageVisibility Visibility { get; init; } = MessageVisibility.Always;

		/// <summary>
		/// Gets or sets the list of users (logins) or agents (guids) to whom the message is visible.
		/// If empty, it means that the message is visible to all.
		/// </summary>
		public ImmutableList<string> VisibleTo { get; init; } = [];

		/// <summary>
		/// Gets or sets a value indicating whether a <see cref="VisibleTo"/> is a whitelist filter.
		/// </summary>
		public bool IsVisibleToWhiteList { get; init; }

		/// <summary>
		/// Gets a value indicating whether this input may run a slash command. When <see langword="false"/> the content
		/// is inserted verbatim: no token extraction, no execution and no escape removal. Every producer sets it; the
		/// desktop input and the WebUI pass <see langword="true"/> until a participant-rights model exists.
		/// </summary>
		public bool CanExecuteCommand { get; init; } = true;
	}
}