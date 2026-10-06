using System.ComponentModel;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// Creates the delegate tools used by agent tasks to call sub-agents.
	/// The tools operate on the pre-resolved sub-agent descriptors of the task
	/// and do not require user confirmation.
	/// </summary>
	public sealed class AgentSubAgentTools
	{
		private readonly IAgentTaskExecutor _agentTaskExecutor;
		private readonly ISubAgentTaskParamsResolver _paramsResolver;
		private readonly AgentTaskLaunchParameters _sourceParameters;
		private readonly IReadOnlyList<TaskSubAgentDescriptor> _subAgents;

		/// <summary>
		/// Initializes a new instance with the task executor, the sub-agent parameter resolver
		/// and the sub-agents available to the task.
		/// </summary>
		/// <param name="agentTaskExecutor">The executor used to launch the sub-agent tasks.</param>
		/// <param name="paramsResolver">The resolver used to build launch parameters for a sub-agent call.</param>
		/// <param name="sourceParameters">The launch parameters of the calling task.</param>
		/// <param name="subAgents">The list of sub-agents available to the calling task.</param>
		public AgentSubAgentTools(IAgentTaskExecutor agentTaskExecutor, ISubAgentTaskParamsResolver paramsResolver,
			AgentTaskLaunchParameters sourceParameters, IReadOnlyList<TaskSubAgentDescriptor> subAgents)
		{
			_agentTaskExecutor = agentTaskExecutor;
			_paramsResolver = paramsResolver;
			_sourceParameters = sourceParameters;
			_subAgents = subAgents;
		}

		/// <summary>
		/// Creates the sub-agent calling tool available to the task:
		/// a single <c>agent-callsub</c> tool operating on the task's sub-agent whitelist.
		/// </summary>
		/// <returns>The list of agent tools.</returns>
		public ImmutableList<AgentTool> CreateSubAgentCallTools()
		{
			var tools = ImmutableList.CreateBuilder<AgentTool>();
			if (_subAgents.Count > 0)
			{
				AddTool(tools, "agent-callsub", """
					Calls a sub-agent available in the current task with the provided input.
					Only the sub-agents listed in the task's sub-agent set can be called.
					""", CallSubAgentAsync);
			}
			return tools.ToImmutable();
		}

		private static void AddTool(ImmutableList<AgentTool>.Builder tools, string name, string description, Delegate executor)
		{
			tools.Add(new DelegateAgentTool(name, null, description, executor));
		}

		private async Task<AgentToolCallResult> CallSubAgentAsync(
			[Description("The name of the predefined sub-agent to call")] string agentName,
			[Description("The user message to send to the sub-agent")] string input,
			[Description("""
				If true - waits for end of execution and returns the contents of last message.
				If false - returns agent task ID immediately, the agent will continue to run in the background.
				""")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var descriptor = _subAgents.FirstOrDefault(a => a.Name == agentName);
			if (descriptor is null)
				return Error($"Sub-agent '{agentName}' is not available in this task.");

			try
			{
				var parameters = _paramsResolver.Resolve(_sourceParameters, descriptor,
					[new AgentUserMessage { Content = input }], out var errors, GetChatSubAgentPolicy());
				if (errors.Count > 0)
					return Error(string.Join(Environment.NewLine, errors));

				var ct = wait ? cancellationToken : CancellationToken.None;
				var agentTask = _agentTaskExecutor.Execute(parameters, ct);

				if (wait)
				{
					await agentTask;
					return Success(string.IsNullOrWhiteSpace(agentTask.LastGeneratedContent) ?
						"Sub-agent did not generate any content." : agentTask.LastGeneratedContent);
				}

				return Success($"Sub-agent launched with task ID {agentTask.Id}.");
			}
			catch (Exception ex)
			{
				return Error("An error occurred while calling the sub-agent: " + ex.Message);
			}
		}

		/// <summary>
		/// Reads the chat-level sub-agent tool behaviour policy, if the task is attached to a chat.
		/// </summary>
		private ToolPolicyMask? GetChatSubAgentPolicy()
		{
			var chat = _sourceParameters.TriggeredChat;
			if (chat is null)
				return null;

			var settings = chat.Services.GetService<IChatSettingsService>();
			return settings?.Settings.SubAgents.GetEffectivePolicy();
		}

		private static AgentToolCallResult Success(string content) => new() { Success = true, Content = content };

		private static AgentToolCallResult Error(string content) => new() { Success = false, Content = content };
	}
}
