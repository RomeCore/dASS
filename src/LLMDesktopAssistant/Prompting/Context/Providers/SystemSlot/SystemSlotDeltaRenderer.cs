using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// Delta renderer of the system slot section: renders the diff as a clean unified diff
	/// (no hunk headers, hunks separated by an ellipsis line).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SystemSlotSectionDelta>))]
	public class SystemSlotDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionDeltaRenderer<SystemSlotSectionDelta>
	{
		/// <inheritdoc/>
		public string Render(SystemSlotSectionDelta delta)
		{
			return templates.GetTextTemplate("system_slot_system_section_delta").Render(new
			{
				diff = delta.Diff.ToCleanString()
			});
		}
	}
}
