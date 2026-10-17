using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Agents.Tasks
{
	public class DirectTaskSubAgentDescriptor : TaskSubAgentDescriptor
	{
		/// <summary>
		/// The system prompt to use for agent execution.
		/// </summary>
		public required string SystemPrompt { get; init; }

		/// <summary>
		/// The model to use for generating responses. If not specified, the default model will be used.
		/// </summary>
		public string? Model { get; init; }

		/// <summary>
		/// The list of tools available to the agent during the task.
		/// </summary>
		public ImmutableList<IToolDescriptor> Tools { get; init; } = [];

		/// <summary>
		/// The list of skills available to the agent during the task.
		/// </summary>
		public ImmutableList<AgentSkill> Skills { get; init; } = [];

		/// <summary>
		/// The memory blocks available to the task.
		/// </summary>
		public ImmutableList<TaskMemoryBlock> MemoryBlocks { get; init; } = [];

		/// <summary>
		/// The list of sub-agents available to the task.
		/// </summary>
		public ImmutableList<TaskSubAgentDescriptor> SubAgents { get; init; } = [];
	}
}
