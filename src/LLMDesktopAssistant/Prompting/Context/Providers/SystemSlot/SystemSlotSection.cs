namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// The system slot section: the system prompt text and its components.
	/// </summary>
	public class SystemSlotSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SystemSlotSectionState, SystemSlotSectionDelta>(services)
	{
		public override string Discriminator => "system-slot";
	}
}
