using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	public static class AgentManagementServiceExtensions
	{
		extension(IAgentManagementService service)
		{
			public ChatAgentDescriptor GetSenderAgentDescriptor(ChatMessage message)
			{
				if (message is not AssistantMessage am)
					throw new ArgumentException("Message must be an AssistantMessage.");
				return service.GetAgentDescriptor(am.SenderAgentId);
			}

			public ChatAgentDescriptor? TryGetSenderAgentDescriptor(ChatMessage message)
			{
				return message is AssistantMessage am ? service.TryGetAgentDescriptor(am.SenderAgentId) : null;
			}
		}
	}
}