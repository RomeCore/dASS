using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The parser for scriptable prompt context addon files (e.g. <c>context/git-status.py</c>,
	/// <c>context/time.lua</c>).
	/// The scripting engine is selected by the file extension; it defines both the frontmatter syntax
	/// (<see cref="IScriptEngineDescriptor.FrontmatterStart"/> /
	/// <see cref="IScriptEngineDescriptor.FrontmatterEnd"/>) and the way the body is executed.
	/// </summary>
	/// <remarks>
	/// The provider is created here without services: a scriptable context resolves its chat-scoped services
	/// from the <see cref="PromptContextExecutionContext"/> passed to it at run time, so the parsed addon stays
	/// safe in the global cache.
	/// </remarks>
	[Service(typeof(IAddonFileParser<PromptContextInfo>))]
	public class PromptContextParser : FrontmatterBasedAddonParser<PromptContextInfo, PromptContextChange, IScriptablePromptContextEngine>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PromptContextParser"/> class.
		/// </summary>
		/// <param name="engines">The scripting engines that provide frontmatter syntax and execution.</param>
		public PromptContextParser(IEnumerable<IScriptablePromptContextEngine> engines)
			: base(engines, engine => engine.Descriptors)
		{
		}

		/// <inheritdoc/>
		/// <remarks>A context yields text, so a plain result is the only channel it consumes.</remarks>
		protected override ScriptFeaturePolicy Features => ScriptFeaturePolicy.PlainOutput;

		/// <inheritdoc/>
		protected override void Populate(PromptContextInfo addon, AddonFrontmatterDocument frontmatter,
			ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			// A context whose extension no engine owns was already reported as fatal by the base.
			if (addon.ScriptLanguage is null)
				return;

			var engine = Resolve(addon).Engine;

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
