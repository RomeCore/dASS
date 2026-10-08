using LLMDesktopAssistant.Addons;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// The change configuration object of a <see cref="SlashCommandInfo"/>, produced when an addon instance is
	/// altered (mirrors <c>PromptContextChange</c>).
	/// </summary>
	/// <remarks>
	/// Empty in v1: commands carry no per-chat change of their own beyond the base <see cref="AddonChangeBase"/>
	/// (enabled/hidden/parameters).
	/// </remarks>
	public class SlashCommandChange : AddonChangeBase
	{
	}
}
