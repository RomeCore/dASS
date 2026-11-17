using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	public interface IPromptNotificationProcessor
	{
		void Process(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext);
	}
}