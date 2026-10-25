// ══════════════════════════════════════════════════════════════════════════════════════
//                                ⚰  R.I.P.  META-TOOLS  ⚰
//
//    Born:  d859a35 "Introduce meta tools"                   2026-04-09
//    Died:  superseded by addon-based scriptable tools       2026-09-12
//    Lived: 156 days · 62 commits touched *MetaTool* paths · 37 commits name them in diffs
//
//    The name is gone, the idea is not: a file `tools/my-tool.lua` (`.alua`, `.py`) is
//    still a tool the LLM can call. It just stopped being a special-cased subsystem and became
//    a first-class citizen of the generic addon infrastructure.
//
//    Spiritual successors (not literal renames; some bodies were absorbed by the addon core):
//      Tools/Meta/IMetaToolEngine.cs              -> Tools/Scripting/IScriptableToolEngine.cs
//      Tools/Meta/IMetaToolEngineDescriptor.cs    -> Scripting/IScriptEngineDescriptor.cs
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
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tools.Scripting
{
	/// <summary>
	/// The parser for scriptable tool addon files (e.g. <c>tools/my-tool.lua</c>, <c>tools/my-tool.py</c>,
	/// <c>tools/my-tool.alua</c>).
	/// The scripting engine is selected by the file extension; it defines both the frontmatter syntax
	/// (<see cref="IScriptEngineDescriptor.FrontmatterStart"/> /
	/// <see cref="IScriptEngineDescriptor.FrontmatterEnd"/>) and the way the tool body is executed.
	/// </summary>
	/// <remarks>
	/// The tool body (everything except the frontmatter) is the execution code that is passed to
	/// <see cref="IScriptableToolEngine.CreateExecutor"/>.
	/// </remarks>
	[Service(typeof(IAddonFileParser<ToolInfo>))]
	public class ScriptableToolParser : FrontmatterBasedAddonParser<ToolInfo, ToolChange, IScriptableToolEngine>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptableToolParser"/> class.
		/// </summary>
		/// <param name="engines">The scripting engines that provide frontmatter syntax and executors.</param>
		public ScriptableToolParser(IEnumerable<IScriptableToolEngine> engines)
			: base(engines, engine => engine.Descriptors)
		{
		}

		/// <inheritdoc/>
		protected override ScriptFeaturePolicy Features =>
			ScriptFeaturePolicy.PlainOutput | ScriptFeaturePolicy.JsonOutput | ScriptFeaturePolicy.Terminal;

		/// <inheritdoc/>
		protected override void Populate(ToolInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			// A tool whose extension no engine owns was already reported as fatal by the base.
			if (addon.ScriptLanguage is null)
				return;

			var engine = Resolve(addon).Engine;

			// === Tool-specific metadata ===

			if (frontmatter.TryRequest("approval-level", ref diagnostic, out ToolApprovalLevel approvalLevel))
				addon.ApprovalLevel = approvalLevel;

			var behaviours = frontmatter.TryRequest("behaviours", ref diagnostic, out ToolBehaviour parsedBehaviours)
				? parsedBehaviours
				: ToolBehaviour.None;

			addon.DefaultExpectedBehaviour = ToolBehaviour.Scriptable | behaviours;

			var argumentSchema = frontmatter.TryRequest("argument-schema", ref diagnostic, out JsonObject parsedArgumentSchema)
				? parsedArgumentSchema
				: new JsonObject
				{
					["type"] = "object",
					["additionalProperties"] = false
				};

			addon.ArgumentSchema = argumentSchema;

			// === Execution ===

			addon.ToolSource = ToolSource.Scriptable;
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
