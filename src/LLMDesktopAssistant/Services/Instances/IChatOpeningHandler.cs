namespace LLMDesktopAssistant.Services.Instances
{
	/// <summary>
	/// Represents a service that handles the opening of a chat instance.
	/// Handlers run after every chat service has been constructed and the history has been loaded,
	/// so they can use all chat services.
	/// </summary>
	public interface IChatOpeningHandler
	{
		/// <summary>
		/// Called when a chat instance has just been opened.
		/// </summary>
		void OnChatOpened();
	}
}
