using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The chat-level counter for prompt state anchor IDs (lazily created on the first anchor).
	/// </summary>
	public class PromptStateAnchorIdCounter : AdditionalChatData
	{
		private int _lastId;
		/// <summary>
		/// The last issued anchor ID.
		/// </summary>
		public int LastId
		{
			get => _lastId;
			set => SetProperty(ref _lastId, value);
		}

		public PromptStateAnchorIdCounter()
		{
			IsVisible = false;
		}
	}
}
