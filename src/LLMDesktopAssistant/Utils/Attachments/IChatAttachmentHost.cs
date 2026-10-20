namespace LLMDesktopAssistant.Utils.Attachments
{
	/// <summary>
	/// A chat object that can host attachments: a chat, a message or a tool call.
	/// </summary>
	/// <remarks>
	/// This is the C# side of the contract the Lua attachment handlers rely on
	/// (<c>ILuaDataAttachmentHandler.TryAttach</c>): a handler does not build a return value,
	/// it converts a Lua-side handle into an <see cref="IChatAttachment"/> and gives it to the host.
	/// </remarks>
	public interface IChatAttachmentHost
	{
		/// <summary>
		/// Adds a payload the host projects into its native form.
		/// </summary>
		/// <param name="attachment">The payload to add.</param>
		void AddAttachment(IChatAttachment attachment);

		/// <summary>
		/// Removes a payload from the host and disposes it.
		/// </summary>
		/// <param name="attachment">The payload to remove.</param>
		/// <returns><see langword="true"/> when the payload was attached to this host.</returns>
		/// <remarks>
		/// A removed payload is disposed: a live attachment (a terminal session, for example) owns
		/// a process, and removing it from the host is the only signal it gets.
		/// </remarks>
		bool RemoveAttachment(IChatAttachment attachment);

		/// <summary>
		/// Removes and disposes every payload attached to the host.
		/// </summary>
		void ClearAttachments();

		/// <summary>
		/// Gets every payload attached to the host, in collection order.
		/// </summary>
		/// <returns>The attached payloads.</returns>
		IEnumerable<IChatAttachment> GetAttachments();
	}
}
