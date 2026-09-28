namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// The tools section: provides the tool definitions for the system prompt.
	/// </summary>
	public class ToolsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<ToolsSectionState, ToolsSectionDelta>(services)
	{
		public override string Discriminator => "tools";
	}
}
