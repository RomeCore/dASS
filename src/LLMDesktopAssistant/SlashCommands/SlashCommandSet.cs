using LLMDesktopAssistant.Addons;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// The chat-level per-command override bag. <c>Changes</c> is keyed by <see cref="SlashCommandInfo.Key"/> (the
	/// fully-qualified command identity), so overriding a command never leaks onto a same-named command of another
	/// namespace.
	/// </summary>
	public class SlashCommandSet : AddonSetConfigurationBase<SlashCommandChange>
	{
	}
}
