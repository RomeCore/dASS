using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The built-in <c>/lua-reset</c> command: drops the chat's shared Lua state and reloads every Lua script addon.
	/// </summary>
	/// <remarks>
	/// The command is <see cref="AddonChangedBase{Self,TChange}.IsFixed"/> so it cannot be disabled - like
	/// <c>/addon-reload</c>, it is the recovery hatch when the Lua state is broken and the user has no other way back
	/// short of restarting the application.
	/// </remarks>
	[NativeSlashCommandProvider]
	public sealed class LuaResetCommandProvider : NativeSlashCommandProvider
	{
		public LuaResetCommandProvider()
		{
			AddCommand(new SlashCommandInfo
			{
				Name = "lua-reset",
				Aliases = ["luareset"],
				NameKey = Locale.GetKey("command.lua_reset.name"),
				DescriptionKey = Locale.GetKey("command.lua_reset.description"),
				IsFixed = true,
				Generate = false,
				ModelFacingMode = ModelFacingMode.Neutral,
				Executor = new LuaResetCommandExecutor()
			});
		}
	}
}
