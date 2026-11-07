using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Keeps one store in the BSON document of a <see cref="LuaStoreChatData"/> row.
	/// </summary>
	public sealed class LuaChatStoreBackend(LuaStoreChatData data) : ILuaStoreBackend
	{
		/// <inheritdoc />
		public string? FilePath => null;

		/// <inheritdoc />
		public LuaTable Load()
		{
			return LuaStoreValueConverter.Restore(data.Values.ToReactiveNodeValue());
		}

		/// <inheritdoc />
		public void Persist(LuaTable snapshot, Action<string>? warn)
		{
			var dropped = new List<string>();
			var converted = LuaStoreValueConverter.ToSnapshot(snapshot, dropped);
			LuaFileStoreBackend.ReportDropped("store.chat", dropped, warn);
			data.Values = converted.ToBsonValue();
		}
	}
}
