using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Tools.Scripting
{
	[Service(typeof(IAddonFileLocator<ToolInfo>))]
	public class ToolFileLocator : AddonFileLocatorBase<ToolInfo>
	{
		private readonly IScriptableToolEngine[] _engines;

		public ToolFileLocator(IEnumerable<IScriptableToolEngine> engines) => _engines = [.. engines];

		public override string[] Folders => ["tools"];

		public override string[] Extensions => field ??= [.. _engines.SelectMany(e => e.Descriptors).SelectMany(d => d.Extensions).Distinct()];

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => null;
	}
}
