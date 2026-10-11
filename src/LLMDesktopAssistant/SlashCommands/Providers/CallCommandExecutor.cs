using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/call &lt;agent&gt;</c>: stamps the produced message with a <see cref="ForcedAgentMarker"/> so the
	/// ordering service forces that chat agent on the next turn, whatever the execution stages would have picked.
	/// </summary>
	/// <remarks>
	/// The agent is the command's whole tail — a required, never-split rest positional whose format provider validates
	/// the name against the chat's agents, converts it to the agent's id and completes it. The caller's generation
	/// intent is left untouched and the message reaches the model as written.
	/// </remarks>
	public sealed class CallCommandExecutor(IAgentManagementService agentManager) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = new()
		{
			RestPositional = new SlashCommandArgument
			{
				Name = Locale.GetKey("command.argument.call.agent"),
				Description = Locale.GetKey("command.argument.call.agent.description"),
				Required = true,
				Format = new AgentNameArgumentFormatProvider(agentManager)
			}
		};

		/// <inheritdoc/>
		public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			// Binding guarantees a required, valid rest positional, so this is a guard rather than a real path.
			if (ctx.Arguments.RestPositional?.Value is not Guid agentId)
				return Task.FromResult(new SlashCommandExecutionResult(
					Generate: false, Error: Locale.GetKey("command.error.agent_not_found")));

			ctx.Message.AdditionalData.Add(new ForcedAgentMarker { AgentId = agentId });

			return Task.FromResult(SlashCommandExecutionResult.Ok());
		}
	}
}
