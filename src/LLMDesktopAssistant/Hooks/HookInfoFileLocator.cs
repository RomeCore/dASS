using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Hooks
{
	[Service(typeof(IAddonFileLocator<HookInfo>))]
	public class HookInfoFileLocator : AddonFileLocatorBase<HookInfo>
	{
		private readonly IScriptableHookEngine[] _engines;

		public HookInfoFileLocator(IEnumerable<IScriptableHookEngine> engines) => _engines = [.. engines];

		public override string[] Folders => ["hooks"];

		public override string[] Extensions =>
			field ??= [.. _engines.SelectMany(e => e.Descriptors).SelectMany(d => d.Extensions).Distinct()];

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => null;
	}
}
