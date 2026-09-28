namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// The sub-agents section: provides the available sub-agents for the system prompt.
	/// </summary>
	public class SubAgentsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SubAgentsSectionState, SubAgentsSectionDelta>(services)
	{
		public override string Discriminator => "sub-agents";
	}
}
