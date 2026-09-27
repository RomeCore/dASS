namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	public enum PromptContextMode
	{
		/// <summary>
		/// The complex mode for system prompt. System prompt is being frozen lazily (anchor is placed in the history),
		/// then LLM receives notifications when system prompt sections are updated. System prompt anchor refreshes when new
		/// <see cref="ContextCheckpoint"/> is added into the history and it blocks existing anchors.
		/// </summary>
		Hybrid,

		/// <summary>
		/// The system prompt is re-rendered each time when LLM completion request is being built.
		/// </summary>
		Dynamic,

		/// <summary>
		/// The system prompt is frozen and stays in the configuration until user explicitly updates it or changes to other mode.
		/// </summary>
		Static,
	}
}
