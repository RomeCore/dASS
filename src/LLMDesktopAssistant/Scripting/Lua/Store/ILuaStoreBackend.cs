using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Persists the whole key table of a <see cref="LuaStore"/> somewhere, or nowhere.
	/// </summary>
	/// <remarks>
	/// The live table is the source of truth; a backend only receives its snapshots. That is why every
	/// method is whole-store: there is no per-key persistence to get out of sync with the cache.
	/// </remarks>
	public interface ILuaStoreBackend
	{
		/// <summary>
		/// Gets the absolute path of the backing file, or <see langword="null"/> when the backend is not a file.
		/// </summary>
		string? FilePath { get; }

		/// <summary>
		/// Loads the persisted snapshot, or an empty table when nothing has been persisted yet.
		/// </summary>
		/// <returns>The live key table for the store.</returns>
		LuaTable Load();

		/// <summary>
		/// Writes the whole key table.
		/// </summary>
		/// <param name="data">The live key table. It is read, never mutated.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		void Persist(LuaTable data, Action<string>? warn);
	}
}
