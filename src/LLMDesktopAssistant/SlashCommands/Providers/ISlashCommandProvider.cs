using LLMDesktopAssistant.Addons.Sources;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// A source of slash commands. Providers are chat-scoped addon sources: the command set collector subscribes
	/// to their <see cref="IAdditionalAddonSource{T}.Addons"/> collection and patches only the affected keys.
	/// </summary>
	public interface ISlashCommandProvider : IAdditionalAddonSource<SlashCommandInfo>
	{
	}
}
