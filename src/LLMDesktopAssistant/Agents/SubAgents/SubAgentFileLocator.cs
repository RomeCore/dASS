using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// The locator that finds sub-agent addons (<c>agent.md</c>/<c>AGENT.md</c>) in addon packs and folders.
	/// </summary>
	[Service(typeof(IAddonFileLocator<SubAgentInfo>))]
	public class SubAgentFileLocator : AddonFileLocatorBase<SubAgentInfo>
	{
		public override string[] Folders => ["agents"];

		public override string[] Extensions => [".md", ".mdx"];

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => "AGENT";
	}
}
