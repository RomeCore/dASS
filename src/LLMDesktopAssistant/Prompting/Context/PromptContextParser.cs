using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The parser for scriptable prompt context addon files (e.g. <c>context/git-status.py</c>,
	/// <c>context/time.lua</c>).
	/// The scripting engine is selected by the file extension; it defines both the frontmatter syntax
	/// (<see cref="IScriptablePromptContextEngineDescriptor.FrontmatterStart"/> /
	/// <see cref="IScriptablePromptContextEngineDescriptor.FrontmatterEnd"/>) and the way the body is executed.
	/// </summary>
	/// <remarks>
	/// The provider is created here without services: a scriptable context resolves its chat-scoped services
	/// from the <see cref="PromptContextExecutionContext"/> passed to it at run time, so the parsed addon stays
	/// safe in the global cache.
	/// </remarks>
	[Service(typeof(IAddonFileParser<PromptContextInfo>))]
	public class PromptContextParser : FrontmatterBasedAddonParser<PromptContextInfo, PromptContextChange>
	{
		private static readonly StringComparer _extensionComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private readonly Dictionary<string, IScriptablePromptContextEngine> _enginesByExtension;

		/// <summary>
		/// Initializes a new instance of the <see cref="PromptContextParser"/> class.
		/// </summary>
		/// <param name="engines">The scripting engines that provide frontmatter syntax and execution.</param>
		public PromptContextParser(IEnumerable<IScriptablePromptContextEngine> engines)
		{
			_enginesByExtension = engines
				.SelectMany(engine => engine.Descriptor.Extensions.Select(ext => (Extension: ext, Engine: engine)))
				.ToDictionary(x => x.Extension, x => x.Engine, _extensionComparer);
		}

		/// <summary>
		/// Gets the scripting engine that handles files with the given path extension.
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown when no engine is registered for the extension.</exception>
		private IScriptablePromptContextEngine GetEngine(string path)
		{
			var extension = Path.GetExtension(path);
			if (_enginesByExtension.TryGetValue(extension, out var engine))
				return engine;

			throw new NotSupportedException(
				$"No scriptable prompt context engine is registered for the '{extension}' extension (file: '{path}'). " +
				$"Supported extensions: {string.Join(", ", _enginesByExtension.Keys)}.");
		}

		/// <inheritdoc/>
		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			var descriptor = GetEngine(fileInfo.Path).Descriptor;

			return new AddonParserDescriptor
			{
				FrontmatterStart = descriptor.FrontmatterStart,
				FrontmatterEnd = descriptor.FrontmatterEnd,
				// A context is useless without metadata, and its body is code, not markdown.
				RequiresFrontmatter = true,
				UseMarkdownFallback = false
			};
		}

		/// <inheritdoc/>
		protected override void Populate(PromptContextInfo addon, AddonFrontmatterDocument frontmatter,
			ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			var engine = GetEngine(addon.Path ?? throw new InvalidOperationException("The context path is not set."));

			if (!frontmatter.TryRequest("kind", ref diagnostic, out PromptContextProviderKind kind))
				kind = PromptContextProviderKind.Supersede;

			if (kind is not PromptContextProviderKind.Supersede and not PromptContextProviderKind.LiveTail)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					IsFatal = true,
					Codes = AddonDiagnosticCode.InvalidKind,
					Messages = ["Kind is invalid, must be 'supersede' or 'live-tail'."]
				});
				return;
			}

			int timeoutMs = ScriptPromptContextHelper.DefaultTimeoutMs;
			if (frontmatter.TryRequest("timeout", ref diagnostic, out int parsedTimeout))
				timeoutMs = Math.Clamp(parsedTimeout, 1, ScriptPromptContextHelper.MaxTimeoutMs);
			addon.TimeoutMs = timeoutMs;

			if (!frontmatter.TryRequest("variability", ref diagnostic, out PromptSectionVariability variability))
				addon.Variability = kind is PromptContextProviderKind.LiveTail
					? PromptSectionVariability.PerRequest
					: PromptSectionVariability.Frequent;
			else
				addon.Variability = variability;

			// The kind is parsed, not derived from the provider type: the engine reads it to decide which
			// provider to build, so deriving it from the provider would be circular.
			addon.Kind = kind;
			addon.Provider = engine.CreateProvider(addon);
		}

		/// <inheritdoc/>
		protected override bool IsValidName(string name)
		{
			if (string.IsNullOrEmpty(name) || name.Length > 64)
				return false;

			foreach (var ch in name)
			{
				if (!char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_')
					return false;
			}

			return true;
		}

		/// <inheritdoc/>
		protected override string NormalizeName(string name)
		{
			var builder = new StringBuilder(name.Length);
			foreach (var ch in name)
				builder.Append(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');

			var normalized = builder.ToString().Trim('-', '_');
			if (normalized.Length == 0)
				return "unknown";

			return normalized.Length > 64 ? normalized[..64] : normalized;
		}
	}
}
