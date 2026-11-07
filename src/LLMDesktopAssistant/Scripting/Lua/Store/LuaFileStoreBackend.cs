using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Keeps one store in a JSON file, rewritten as a whole on every change.
	/// </summary>
	public sealed class LuaFileStoreBackend(string path) : ILuaStoreBackend
	{
		/// <inheritdoc />
		public string? FilePath => path;

		/// <inheritdoc />
		public LuaTable Load()
		{
			return LuaStoreValueConverter.Restore(LuaStoreJson.ReadFile(path, quarantine: true));
		}

		/// <inheritdoc />
		public void Persist(LuaTable data, Action<string>? warn)
		{
			var dropped = new List<string>();
			var snapshot = LuaStoreValueConverter.ToSnapshot(data, dropped);
			ReportDropped("store.file", dropped, warn);
			LuaStoreJson.WriteFile(path, snapshot);
		}

		internal static void ReportDropped(string storeName, List<string> dropped, Action<string>? warn)
		{
			foreach (var path in dropped)
				warn?.Invoke($"{storeName}: dropped a value that cannot be persisted at '{path}'");
		}
	}
}
