using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Owns the <c>store.chat</c> store of one chat.
	/// </summary>
	/// <remarks>
	/// The data lives in a <see cref="LuaStoreChatData"/> row of the chat itself, so it is created lazily
	/// on first use and persists exactly as long as the chat does.
	/// </remarks>
	[ChatService]
	public class LuaChatStoreService(Chat chat)
	{
		private readonly Lock _lock = new();
		private LuaStore? _store;

		/// <summary>
		/// Gets the store persisted in the current chat.
		/// </summary>
		/// <returns>The store of the current chat.</returns>
		public LuaStore GetStore()
		{
			lock (_lock)
			{
				if (_store is not null)
					return _store;

				var data = chat.AdditionalData.TryGet<LuaStoreChatData>() ?? CreateData();
				_store = new LuaStore("store.chat", new LuaChatStoreBackend(data));
				return _store;
			}
		}

		private LuaStoreChatData CreateData()
		{
			var data = new LuaStoreChatData();
			chat.AdditionalData.Add(data);
			return data;
		}
	}
}
