using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The locator that finds scriptable prompt context addons in addon packs and folders.
	/// </summary>
	[Service(typeof(IAddonFileLocator<PromptContextInfo>))]
	public class PromptContextFileLocator : AddonFileLocatorBase<PromptContextInfo>
	{
		private readonly IScriptablePromptContextEngine[] _engines;

		public PromptContextFileLocator(IEnumerable<IScriptablePromptContextEngine> engines) => _engines = [.. engines];

		public override string[] Folders => ["context"];

		public override string[] Extensions =>
			field ??= [.. _engines.SelectMany(e => e.Descriptors).SelectMany(d => d.Extensions).Distinct()];

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => null;
	}
}
