using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Prompting.Context
{
	public class PromptSupersedeStampMessageData : AdditionalChatData
	{
		public IReadOnlyList<PromptSupersedeStampBase> Stamps { get; init; } = [];

		public PromptSupersedeStampMessageData()
		{
			IsVisible = false;
		}
	}
}
