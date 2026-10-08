namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// A source of slash commands. Providers are chat-scoped and are pulled by the command set collector through
	/// <c>GetAdditionalAddons()</c>.
	/// </summary>
	public interface ISlashCommandProvider
	{
		/// <summary>
		/// Builds the commands this provider contributes.
		/// </summary>
		IEnumerable<SlashCommandInfo> GetCommands();
	}
}
