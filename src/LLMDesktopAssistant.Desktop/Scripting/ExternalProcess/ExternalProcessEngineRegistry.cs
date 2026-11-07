using System.IO;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// Maps a script file to its language engine and descriptor. The kind adapters use it so they never build their
	/// own extension tables; the parser side resolves the same way through the kind interfaces' <c>Descriptors</c>.
	/// </summary>
	[Service]
	public class ExternalProcessEngineRegistry
	{
		private static readonly StringComparer _extensionComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private readonly Dictionary<string, (IExternalProcessScriptEngine Engine, IScriptEngineDescriptor Descriptor)>
			_enginesByExtension = new(_extensionComparer);

		private readonly List<IScriptEngineDescriptor> _descriptors = [];

		private readonly List<(string Name, IExternalProcessScriptEngine Engine, IScriptEngineDescriptor Descriptor)>
			_languages = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalProcessEngineRegistry"/> class.
		/// </summary>
		public ExternalProcessEngineRegistry(IEnumerable<IExternalProcessScriptEngine> engines)
		{
			foreach (var engine in engines)
			{
				var descriptor = engine.Descriptor;
				if (!_descriptors.Contains(descriptor))
				{
					_descriptors.Add(descriptor);

					// A language with no extensions cannot run here (batch off Windows), so it gets no name either.
					if (descriptor.Extensions.Length > 0)
						_languages.Add((descriptor.Language.ToString().ToLowerInvariant(), engine, descriptor));
				}

				foreach (var extension in descriptor.Extensions)
				{
					// The first engine to claim an extension wins: registration order is the priority order.
					_enginesByExtension.TryAdd(extension, (engine, descriptor));
				}
			}
		}

		/// <summary>
		/// The extensions served by the registered engines.
		/// </summary>
		public string[] Extensions => [.. _enginesByExtension.Keys];

		/// <summary>
		/// The descriptors of the registered engines, in registration order. A kind adapter answers with this list.
		/// </summary>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors => _descriptors;

		/// <summary>
		/// The language names this platform can run, in registration order.
		/// </summary>
		public IReadOnlyList<string> LanguageNames => [.. _languages.Select(language => language.Name)];

		/// <summary>
		/// Resolves the engine behind a language name (<c>python</c>, <c>bash</c>, ...).
		/// </summary>
		public (IExternalProcessScriptEngine Engine, IScriptEngineDescriptor Descriptor)? ResolveLanguage(string name)
		{
			foreach (var language in _languages)
			{
				if (string.Equals(language.Name, name, StringComparison.Ordinal))
					return (language.Engine, language.Descriptor);
			}

			return null;
		}

		/// <summary>
		/// Resolves the engine and the descriptor that own the given file.
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown when no engine is registered for the file's extension.</exception>
		public (IExternalProcessScriptEngine Engine, IScriptEngineDescriptor Descriptor) Resolve(string path)
		{
			var extension = Path.GetExtension(path);
			if (_enginesByExtension.TryGetValue(extension, out var entry))
				return entry;

			throw new NotSupportedException(
				$"No out-of-process script engine is registered for the '{extension}' extension (file: '{path}'). " +
				$"Supported extensions: {string.Join(", ", _enginesByExtension.Keys)}.");
		}
	}
}
