using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// Renders the sub-agents section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SubAgentsSectionState>))]
	public class SubAgentsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<SubAgentsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SubAgentsSectionState state)
		{
			return templates.GetTextTemplate("sub_agents_system_section").Render(new
			{
				sub_agents = state.Items.Count > 0
					? state.Items.Select(s => new
					{
						name = s.Name,
						description = s.Description
					}).ToArray()
					: null,
				hidden_sub_agents = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			});
		}
	}
}
