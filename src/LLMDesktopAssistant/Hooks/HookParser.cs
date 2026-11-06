using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Hooks
{
	[Service(typeof(IAddonFileParser<HookInfo>))]
	public class HookParser : FrontmatterBasedAddonParser<HookInfo, HookChange, IScriptableHookEngine>
	{
		public HookParser(IEnumerable<IScriptableHookEngine> engines)
			: base(engines)
		{
		}

		/// <inheritdoc/>
		protected override ScriptFeaturePolicy Features =>
			ScriptFeaturePolicy.JsonOutput;

		/// <inheritdoc/>
		protected override void Populate(HookInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			if (addon.ScriptLanguage is null)
				return;

			var engine = Resolve(addon).Engine;

			if (!frontmatter.TryRequest("hook-type", ref diagnostic, out string hookType))
				addon.HookType = hookType;
			if (frontmatter.TryRequest("hook-name", ref diagnostic, out string hookName))
				addon.HookName = hookName;
			if (frontmatter.TryRequest("execution-flow", ref diagnostic, out HookExecutionFlow executionFlow))
				addon.ExecutionFlow = executionFlow;
			if (frontmatter.TryRequest("timeout", ref diagnostic, out int timeoutMs))
				addon.TimeoutMs = timeoutMs;

			addon.Executor = engine.CreateExecutor(addon);
		}
	}
}
