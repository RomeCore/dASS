using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Owns the <c>store.session</c> store of one chat.
	/// </summary>
	/// <remarks>
	/// The store lives in this chat-scoped service, not in the Lua state, so it survives a script reload
	/// (which clears the globals) and dies with the chat.
	/// </remarks>
	[ChatService]
	public class LuaSessionStoreService
	{
		/// <summary>
		/// Gets the in-memory store of the current chat.
		/// </summary>
		public LuaStore Session { get; } = new("store.session", null);
	}
}
