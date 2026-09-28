using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// Renders the system slot section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SystemSlotSectionState>))]
	public class SystemSlotStateRenderer : IPromptSectionStateRenderer<SystemSlotSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SystemSlotSectionState state) => state.Text;
	}
}
