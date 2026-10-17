// ══════════════════════════════════════════════════════════════════════════════════════
//                                ⚰  R.I.P.  META-TOOLS  ⚰
//
//    Born:  d859a35 "Introduce meta tools"                   2026-04-09
//    Died:  superseded by addon-based scriptable tools       2026-09-12
//    Lived: 156 days · 62 commits touched *MetaTool* paths · 37 commits name them in diffs
//
//    The name is gone, the idea is not: a file `tools/my-tool.lua` (`.alua`, `.py`, `.csx`) is
//    still a tool the LLM can call. It just stopped being a special-cased subsystem and became
//    a first-class citizen of the generic addon infrastructure.
//
//    Spiritual successors (not literal renames; some bodies were absorbed by the addon core):
//      Tools/Meta/IMetaToolEngine.cs              -> Tools/Scripting/IScriptableToolEngine.cs
//      Tools/Meta/IMetaToolEngineDescriptor.cs    -> Tools/Scripting/IScriptableToolEngineDescriptor.cs
//      Tools/Meta/MetaToolParser.cs               -> Tools/Scripting/ScriptableToolParser.cs   <- this file
//      Tools/Meta/MetaToolInfo.cs                 -> Tools/ToolInfo.cs
//      Tools/Meta/MetaToolLoader.cs               -> Addons/Loading/AddonFileCachedLoader.cs
//      Tools/Meta/MetaToolLocator.cs              -> Addons/Loading/AddonFileLocatorBase.cs
//      Tools/Meta/IMetaToolManagementService.cs   -> Addons/Management/IAddonManager.cs
//      Tools/Meta/MetaToolDiagnostic(Codes).cs    -> Addons/AddonDiagnostic(Codes).cs
//      Tools/Meta/MetaToolSource.cs               -> Addons/AddonSource.cs
//      Tools/Implementations/MetaToolModule.cs    -> (nothing: the LLM edits tool addon files directly)
//      LLM/Settings/MetaToolSourcesSettings.cs    -> tools addon sources (ChatToolSettings + AddonPackSource)
//      LLM/MVVM/Settings/MetaTool*                -> (nothing: one generic addon settings UI)
//      (new, no ancestor)                         -> Tools/Scripting/ToolAddonTypeDescriptor.cs ("tools" type)
//
//    Last relics of the name still standing (scavenge them from git history if they ever fall):
//      Utils/MetaToolHumanizedEnumNames.cs · Utils/Directories.cs (Metatools folder)
// ══════════════════════════════════════════════════════════════════════════════════════

using System.Text;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tools.Scripting
{
	/// <summary>
	/// The parser for scriptable tool addon files (e.g. <c>tools/my-tool.lua</c>, <c>tools/my-tool.py</c>,
	/// <c>tools/my-tool.csx</c>, <c>tools/my-tool.alua</c>).
	/// The scripting engine is selected by the file extension; it defines both the frontmatter syntax
	/// (<see cref="IScriptableToolEngineDescriptor.FrontmatterStart"/> /
	/// <see cref="IScriptableToolEngineDescriptor.FrontmatterEnd"/>) and the way the tool body is executed.
	/// </summary>
	/// <remarks>
	/// The tool body (everything except the frontmatter) is the execution code that is passed to
	/// <see cref="IScriptableToolEngine.CreateExecutor"/>.
	/// </remarks>
	[Service(typeof(IAddonFileParser<ToolInfo>))]
	public class ScriptableToolParser : FrontmatterBasedAddonParser<ToolInfo, ToolChange>
	{
		private static readonly StringComparer _extensionComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private readonly Dictionary<string, IScriptableToolEngine> _enginesByExtension;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptableToolParser"/> class.
		/// </summary>
		/// <param name="engines">The scripting engines that provide frontmatter syntax and executors.</param>
		public ScriptableToolParser(IEnumerable<IScriptableToolEngine> engines)
		{
			_enginesByExtension = engines
				.SelectMany(engine => engine.Descriptor.Extensions.Select(ext => (Extension: ext, Engine: engine)))
				.ToDictionary(x => x.Extension, x => x.Engine, _extensionComparer);
		}

		/// <summary>
		/// Gets the scripting engine that handles files with the given path extension.
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown when no engine is registered for the extension.</exception>
		private IScriptableToolEngine GetEngine(string path)
		{
			var extension = Path.GetExtension(path);
			if (_enginesByExtension.TryGetValue(extension, out var engine))
				return engine;

			throw new NotSupportedException(
				$"No scriptable tool engine is registered for the '{extension}' extension (file: '{path}'). " +
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
				// Scriptable tools are useless without metadata (description, argument schema, ...).
				RequiresFrontmatter = true,
				// The body is code, not markdown, so '#'-heading fallbacks must not be used here.
				UseMarkdownFallback = false
			};
		}

		/// <inheritdoc/>
		protected override void Populate(ToolInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			IScriptableToolEngine engine;
			try
			{
				engine = GetEngine(addon.Path ?? throw new InvalidOperationException("The tool path is not set."));
			}
			catch (Exception ex)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					IsFatal = true,
					Codes = AddonDiagnosticCode.GeneralParsingError,
					Exceptions = [ex]
				});
				return;
			}

			addon.ScriptLanguage = engine.Language;

			// === Tool-specific metadata ===

			if (frontmatter.TryRequest("approval-level", ref diagnostic, out ToolApprovalLevel approvalLevel))
				addon.ApprovalLevel = approvalLevel;

			var behaviours = frontmatter.TryRequest("behaviours", ref diagnostic, out ToolBehaviour parsedBehaviours)
				? parsedBehaviours
				: ToolBehaviour.None;

			addon.DefaultExpectedBehaviour = ToolBehaviour.Meta | behaviours;

			var argumentSchema = frontmatter.TryRequest("argument-schema", ref diagnostic, out JsonObject parsedArgumentSchema)
				? parsedArgumentSchema
				: new JsonObject
				{
					["type"] = "object",
					["additionalProperties"] = false
				};

			addon.ArgumentSchema = argumentSchema;

			// === Execution ===

			addon.ToolSource = ToolSource.Meta;
			addon.Main = engine.CreateExecutor(addon);
		}

		/// <inheritdoc/>
		protected override bool IsValidName(string name)
		{
			return ToolName.CheckValid(name);
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
