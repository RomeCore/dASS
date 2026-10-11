using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The built-in <c>/addon-reload</c> command: forces a reload of every addon manager (app and chat) and rescans the
	/// addon packs on disk, so freshly edited skills, sub-agents, tools and templates are picked up without restarting
	/// the application.
	/// </summary>
	/// <remarks>
	/// The command is <see cref="AddonChangedBase{Self,TChange}.IsFixed"/> so it cannot be disabled — it is the recovery
	/// hatch when the addon state is broken. It
	/// <see cref="SlashCommandInfo.Generate"/>s nothing and its message is
	/// <see cref="ModelFacingMode.Neutral"/> so the reload never leaks into the model context.
	/// </remarks>
	[NativeSlashCommandProvider]
	public sealed class AddonReloadCommandProvider : NativeSlashCommandProvider
	{
		public AddonReloadCommandProvider()
		{
			AddCommand(new SlashCommandInfo
			{
				Name = "addon-reload",
				Aliases = ["reload"],
				NameKey = Locale.GetKey("command.addon_reload.name"),
				DescriptionKey = Locale.GetKey("command.addon_reload.description"),
				IsFixed = true,
				Generate = false,
				ModelFacingMode = ModelFacingMode.Neutral,
				Executor = new AddonReloadCommandExecutor()
			});
		}
	}
}
