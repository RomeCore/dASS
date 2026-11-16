using LLMDesktopAssistant.Tools.Execution.Domain;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Messages;

namespace LLMDesktopAssistant.Agents.Tasks
{
	internal class AgentTaskExecutionInfo
	{
		public required LLModel Model { get; init; }

		public required ImmutableDictionary<string, IToolDescriptor> Tools { get; init; }

		public required List<IMessage> NativeMessages { get; init; }
	}
}
