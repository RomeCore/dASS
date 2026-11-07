using System.Collections.Concurrent;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Owns the stores that live outside a chat scope: the shared <c>store.appsession</c> store and
	/// one <c>store.file</c> store per absolute path.
	/// </summary>
	/// <remarks>
	/// A path always maps to the same instance, in every chat, for the whole application lifetime.
	/// On Windows paths compare case-insensitively, exactly like the file system does.
	/// </remarks>
	[Service]
	public class LuaStoreRegistry
	{
		private readonly ConcurrentDictionary<string, LuaStore> _files = new(
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

		/// <summary>
		/// Gets the in-memory store shared by every open chat.
		/// </summary>
		public LuaStore AppSession { get; } = new("store.appsession", null);

		/// <summary>
		/// Gets the store backed by the given file, creating it on first use.
		/// </summary>
		/// <param name="fullPath">The absolute path of the backing file.</param>
		/// <returns>The store shared by every caller that names the same path.</returns>
		public LuaStore GetFileStore(string fullPath)
		{
			return _files.GetOrAdd(fullPath,
				path => new LuaStore($"store.file({path})", new LuaFileStoreBackend(path)));
		}
	}
}
