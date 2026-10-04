using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Scripting.Lua.API;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Implementations;
using RCLargeLanguageModels;

namespace LLMDesktopAssistant.Agents.Tasks
{
	public class AgentTaskLaunchParameters
	{
		/// <summary>
		/// The display name of the task to be launched. Used for identifying by the user in the dispatcher.
		/// </summary>
		public required string? TaskName { get; init; }

		/// <summary>
		/// The message that this task is associated with.
		/// This could be a message containing the tool call that triggered this task
		/// (via <see cref="AgenticToolModule"/> or <see cref="LuaApiAgents"/>).
		/// </summary>
		public ChatMessage? TriggeredMessage { get; init; }

		/// <summary>
		/// The chat that this task is associated with.
		/// </summary>
		public Chat? TriggeredChat { get; init; }

		/// <summary>
		/// The full name of the model to be used for the agent task.
		/// If provided, the model will be taken from <see cref="IModelManager"/>.
		/// </summary>
		public string? ModelName { get; init; }

		/// <summary>
		/// The model instance that will be used for the agent task.
		/// This can be used for custom model configuration (but tools will be overriden) or other purposes.
		/// If not provided, it will be loaded from the <see cref="ModelName"/> property.
		/// </summary>
		public LLModel? Model { get; init; }

		/// <summary>
		/// The execution behaviour of the agent task. This can affect how the task is executed.
		/// </summary>
		public AgentTaskExecutionBehaviour Behaviour { get; init; } = AgentTaskExecutionBehaviour.Normal;

		/// <summary>
		/// The feedback function invoked each time the tool call loop of the task is finished,
		/// i.e. when the model produced a response without tool calls. If the function returns
		/// a non-null user message, the message is added to the conversation and the task
		/// continues execution so the model can respond to it. The function is called again
		/// each time the loop finishes, so the caller must guard against infinite loops,
		/// e.g. by limiting the number of feedback rounds using <see cref="AgentTask.FeedbackIterations"/>.
		/// Supported only for the <see cref="AgentTaskExecutionBehaviour.Normal"/> behaviour.
		/// </summary>
		public Func<AgentTask, CancellationToken, Task<AgentUserMessage?>>? FeedbackFunc { get; init; }

		/// <summary>
		/// The maximum number of parallel tool calls that can be executed at once.
		/// </summary>
		public int MaxParallelToolCalls { get; init; } = 20;

		/// <summary>
		/// The initial set of chat messages to be used for the agent task.
		/// </summary>
		public required ImmutableList<AgentChatMessage> InitialMessages { get; init; }

		/// <summary>
		/// The list of tools available to the agent during the task.
		/// </summary>
		public ImmutableList<AgentTool> Tools { get; init; } = [];

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

		/// <summary>
		/// The behaviour of tools that are automatically approved.
		/// </summary>
		public ToolBehaviour AutoApproveBehaviours { get; init; }

		/// <summary>
		/// The behaviour of tools that are disallowed.
		/// </summary>
		public ToolBehaviour DisallowedBehaviours { get; init; }

		/// <summary>
		/// The time span after which the task should be terminated if it has not completed.
		/// </summary>
		public TimeSpan? TimeOut { get; init; } = TimeSpan.FromMinutes(30);

		/// <summary>
		/// The time span after which the task should be removed from the dispatcher.
		/// If null - it will not be removed. If zero - it will be removed immediately upon completion.
		/// </summary>
		public TimeSpan? CompletionExpiryTime { get; init; } = TimeSpan.FromMinutes(5);
	}
}
