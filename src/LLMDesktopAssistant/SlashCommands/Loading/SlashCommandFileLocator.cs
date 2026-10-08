using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.SlashCommands.Loading
{
	/// <summary>
	/// The locator that finds file-based slash commands in the <c>commands/</c> folder of an addon pack.
	/// </summary>
	/// <remarks>
	/// v1 commands come from providers (skills and sub-agents), not from files, so <see cref="Extensions"/> is empty
	/// and the locator finds nothing. The folder is already declared so that file commands plug in without touching the
	/// addon pipeline.
	/// </remarks>
	[Service(typeof(IAddonFileLocator<SlashCommandInfo>))]
	public class SlashCommandFileLocator : AddonFileLocatorBase<SlashCommandInfo>
	{
		public override string[] Folders => ["commands"];

		public override string[] Extensions => []; // v2: file commands

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => null;
	}
}
