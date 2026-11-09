namespace LLMDesktopAssistant.Services.Instances
{
	/// <summary>
	/// Represents a service that handles the closing of a chat instance.
	/// Handlers run just before all chat services dispose cycle, so they
	/// can use chat services to perform closing hooks and cleanup.
	/// </summary>
	public interface IChatClosingHandler
	{
		/// <summary>
		/// Called when a chat instance is closing.
		/// </summary>
		void OnChatClosing();
	}
}
