namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// Additional chat data that needs the chat's service scope to rebuild its live state after a reload.
	/// </summary>
	/// <remarks>
	/// Neither the chat object that owns this data nor the database loader that creates it knows the chat,
	/// so the scope is stamped by the surface that renders the data
	/// (<see cref="AdditionalChatDataCollectionViewModel"/>), which is created where the chat is at hand.
	/// </remarks>
	public interface IChatServicesAttachable
	{
		/// <summary>
		/// Attaches the chat's service scope, so the data can resolve chat-scoped services lazily.
		/// </summary>
		/// <param name="services">The chat's service scope.</param>
		void AttachChatServices(IServiceProvider services);
	}
}
