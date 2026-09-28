using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// The serializable snapshot of a tool addon tracked by the tools section.
	/// </summary>
	public class ToolItem : AddonSectionItem
	{
		/// <summary>
		/// The tool argument schema (a JSON string).
		/// </summary>
		public string ArgumentSchema { get; set; } = "{}";
	}
}
