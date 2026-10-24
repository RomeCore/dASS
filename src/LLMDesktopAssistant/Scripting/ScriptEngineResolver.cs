using System.IO;

namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Resolves a script file to the engine that owns its extension and that engine's descriptor.
	/// </summary>
	/// <remarks>
	/// The three scriptable kinds have three unrelated engine interfaces, so this is composition rather than a base
	/// class: a parser, a locator or an adapter creates a resolver over its own engine type and gets the extension
	/// table once, instead of every one of them rebuilding it.
	/// </remarks>
	public sealed class ScriptEngineResolver<TEngine> where TEngine : IScriptableAddonEngine
	{
		private static readonly StringComparer _extensionComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private readonly Dictionary<string, (TEngine Engine, IScriptEngineDescriptor Descriptor)> _entries =
			new(_extensionComparer);

		/// <summary>
		/// Builds the extension table from the registered engines.
		/// </summary>
		/// <param name="engines">The engines of one kind.</param>
		/// <param name="descriptors">Reads the descriptors of an engine.</param>
		public ScriptEngineResolver(IEnumerable<TEngine> engines,
			Func<TEngine, IReadOnlyList<IScriptEngineDescriptor>> descriptors)
		{
			foreach (var engine in engines)
			{
				foreach (var descriptor in descriptors(engine))
				{
					foreach (var extension in descriptor.Extensions)
					{
						// The first engine to claim an extension wins: registration order is the priority order.
						_entries.TryAdd(extension, (engine, descriptor));
					}
				}
			}
		}

		/// <summary>
		/// The extensions served by the registered engines, in registration order.
		/// </summary>
		public string[] Extensions => [.. _entries.Keys];

		/// <summary>
		/// Resolves the engine and the descriptor that own the given file.
		/// </summary>
		/// <exception cref="NotSupportedException">No engine is registered for the file's extension.</exception>
		public (TEngine Engine, IScriptEngineDescriptor Descriptor) Resolve(string path)
		{
			var extension = Path.GetExtension(path);
			if (_entries.TryGetValue(extension, out var entry))
				return entry;

			throw new NotSupportedException(
				$"No scriptable engine is registered for the '{extension}' extension (file: '{path}'). " +
				$"Supported extensions: {string.Join(", ", _entries.Keys)}.");
		}
	}
}
