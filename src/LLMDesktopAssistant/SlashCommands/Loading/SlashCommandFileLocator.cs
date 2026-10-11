using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands.Scripting;

namespace LLMDesktopAssistant.SlashCommands.Loading
{
	/// <summary>
	/// The locator that finds file-based slash commands in the <c>commands/</c> folder of an addon pack.
	/// </summary>
	/// <remarks>
	/// The extensions are contributed by the registered <see cref="IScriptableSlashCommandEngine"/>s, so a new engine
	/// (a new language) plugs in without touching this locator.
	/// </remarks>
	[Service(typeof(IAddonFileLocator<SlashCommandInfo>))]
	public class SlashCommandFileLocator : AddonFileLocatorBase<SlashCommandInfo>
	{
		private readonly IScriptableSlashCommandEngine[] _engines;

		public SlashCommandFileLocator(IEnumerable<IScriptableSlashCommandEngine> engines) => _engines = [.. engines];

		public override string[] Folders => ["commands"];

		public override string[] Extensions => field ??= [.. _engines.SelectMany(e => e.Descriptor.Extensions).Distinct()];

		public override bool AllowShortFormat => true;

		public override string? FullFormatName => null;
	}
}
