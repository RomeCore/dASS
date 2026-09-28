using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// The delta of the tools section: tool availability transitions, visibility gains
	/// and definition changes (see <see cref="AddonSectionDelta{TItem, TChange}"/>).
	/// </summary>
	public class ToolsSectionDelta : AddonSectionDelta<ToolItem, ToolChange>
	{
	}
}
