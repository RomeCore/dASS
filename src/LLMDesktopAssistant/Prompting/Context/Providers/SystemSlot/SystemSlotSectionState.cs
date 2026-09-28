namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// The state of the system slot section: the rendered system prompt text and its components.
	/// </summary>
	public class SystemSlotSectionState : PromptSectionStateBase
	{
		private string _text = string.Empty;
		/// <summary>
		/// The rendered text of the system slot.
		/// </summary>
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}
	}
}
