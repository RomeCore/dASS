using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using Material.Icons;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/agent:&lt;name&gt; [input]</c>: launches the named predefined sub-agent exactly like the
	/// <c>agent-callsub</c> tool — fresh launch parameters, the chat-level sub-agent tool policy, the task attached to
	/// the command's message.
	/// </summary>
	/// <remarks>
	/// Fire-and-forget by default; the <c>wait</c> keyed argument makes the command await the sub-agent and inject its
	/// last generated content back onto the message. The command leaves the caller's generation intent untouched.
	/// </remarks>
	public sealed class SubAgentCommandExecutor(
		SubAgentInfo subAgent,
		ISubAgentTaskParamsResolver paramsResolver,
		IAgentTaskExecutor agentTaskExecutor,
		IChatSettingsService chatSettings,
		IToolsetCacheService toolsetCache) : ISlashCommandExecutor
	{
		public const string WaitArgumentKey = "wait";

		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = new()
		{
			RestPositional = new SlashCommandArgument
			{
				Name = Locale.GetKey("command.argument.agent.message"),
				Description = Locale.GetKey("command.argument.agent.message.description")
			},
			Keyed = new Dictionary<string, SlashCommandArgument>
			{
				[SubAgentCommandExecutor.WaitArgumentKey] = new SlashCommandArgument
				{
					Name = Locale.GetKey("command.argument.wait"),
					Description = Locale.GetKey("command.argument.wait.description"),
					Required = false,
					Default = "true",
					Format = SlashCommandBooleanFormatProvider.Instance
				}
			}.ToImmutableDictionary()
		};

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var descriptor = new TaskSubAgentDescriptor { Name = subAgent.Name, Description = subAgent.Description };

			var sourceParameters = new AgentTaskLaunchParameters
			{
				TaskName = subAgent.Name,
				TriggeredChat = ctx.Chat,
				TriggeredMessage = ctx.Message,
				InitialMessages = []
			};

			AgentTaskLaunchParameters parameters;
			try
			{
				// Invalidate the toolset cache - params resolver will use it.
				toolsetCache.Invalidate(agent: null);

				parameters = paramsResolver.Resolve(sourceParameters, descriptor,
					[new AgentUserMessage { Content = ctx.Arguments.RestPositionalArguments }],
					out var errors, chatSettings.Settings.SubAgents.GetEffectivePolicy());

				if (errors.Count > 0)
					return new SlashCommandExecutionResult(false, Locale.GetConstKey(string.Join(Environment.NewLine, errors)));
			}
			catch (KeyNotFoundException)
			{
				// The source addon can drift out of the availability set between command collection and execution.
				return new SlashCommandExecutionResult(false,
					Locale.GetFormattedKey("command.error.sub_agent_not_found", subAgent.Name));
			}

			var wait = ctx.Arguments.Keyed.TryGetValue(WaitArgumentKey, out var waitArgument)
				&& waitArgument.Value is true;

			// Fire-and-forget passes no command token: it is released when the command returns, which would cancel
			// the background task.
			var task = agentTaskExecutor.Execute(parameters, wait ? ct : CancellationToken.None);

			if (!wait)
				return new SlashCommandExecutionResult(true, null, $"sub-agent '{subAgent.Name}' launched",
					ModelFacingMode.Raw);

			await task;

			if (!string.IsNullOrWhiteSpace(task.LastGeneratedContent))
			{
				ctx.Message.AdditionalData.Add(new AdditionalMessageContentPart
				{
					Content = $"""
						[USER HAS LAUNCHED AGENT THAT FINISHED WITH MESSAGE]:

						{task.LastGeneratedContent}
						""",
					ChipTitle = Locale.GetFormattedKey("command.agent.result", subAgent.Name),
					ChipIcon = (VisualIconKind)MaterialIconKind.Robot,
					IsRestorable = false
				});
			}

			return new SlashCommandExecutionResult(true, null, $"sub-agent '{subAgent.Name}' finished",
				ModelFacingMode.Raw);
		}
	}
}
