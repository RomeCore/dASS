using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.ExecutionStages;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	[ChatService(typeof(IAgentOrderingService))]
	public class AgentOrderingService(
		Chat chat,
		IChatSettingsService chatSettings,
		IServiceProvider services
	) : IAgentOrderingService
	{
		public async Task<(Guid, Guid)?> GetNextAgentAsync(CancellationToken cancellationToken = default)
		{
			var stages = chatSettings.Settings.Agents.ExecutionStages
				.ToList();

			// A forced agent wins over everything: it is honoured before the "no enabled stage" guard, before mentions
			// and before the router.
			if (GetForcedAgent(stages) is { } forced)
				return forced;

			if (!stages.Any(s => s.Enabled))
				return null;

			var (roundUserMessages, roundAssistantMessages) = CollectThisRoundMessages();
			var executedInThisRound = CollectExecutedAgents(roundAssistantMessages);
			var (prevAgentId, prevStageId, prevAgentMessages) = GetPreviousExecutionInfo(roundAssistantMessages, stages);
			var (currentStageExecuted, currentStageMessages) = GetCurrentStageInfo(roundAssistantMessages, prevStageId);

			int targetStageIndex = FindTargetStageIndex(prevStageId);

			if (targetStageIndex == -1)
			{
				currentStageMessages = [];
				currentStageExecuted = [];
				targetStageIndex = 0;
			}

			AgentPreExecutionContext BuildContext(
				IReadOnlyList<AssistantMessage> thisStageMessages,
				IReadOnlyList<Guid> executedInThisStage)
			{
				return new AgentPreExecutionContext
				{
					Services = services,
					ThisRoundUserMessages = roundUserMessages,
					ThisRoundAssistantMessages = roundAssistantMessages,
					ExecutedInThisRound = executedInThisRound,
					ThisStageMessages = thisStageMessages,
					ExecutedInThisStage = executedInThisStage,
					PreviousAgentMessages = prevAgentMessages,
					PreviousAgentExecuted = prevAgentId
				};
			}

			for (int i = targetStageIndex; i < stages.Count; i++)
			{
				var stage = stages[i];
				if (!stage.Enabled)
					continue;

				var context = BuildContext(currentStageMessages, currentStageExecuted);
				var nextAgentId = await stage.GetNextAgentAsync(context, cancellationToken);

				if (nextAgentId == null)
				{
					currentStageMessages = [];
					currentStageExecuted = [];
					continue;
				}

				return (nextAgentId.Value, stage.Id);
			}

			return null;
		}

		/// <summary>
		/// Returns the agent forced onto the current round together with the stage to tag its turn with, or
		/// <see langword="null"/> when no live <see cref="ForcedAgentMarker"/> is in play.
		/// </summary>
		/// <remarks>
		/// The marker lives on a user message of the current round, and a round is a run of consecutive user messages
		/// (a marked message followed by plain text, say) — so the whole trailing run is searched, newest message first,
		/// not just the last message. The scan stops at the first assistant (or other) message: a marker behind an
		/// assistant message belongs to a previous round and is inert, even though the marker itself is never consumed.
		/// The forced agent runs even when it belongs to no enabled stage (its turn is tagged with
		/// <see cref="Guid.Empty"/>); only its existence is required.
		/// </remarks>
		private (Guid AgentId, Guid StageId)? GetForcedAgent(IReadOnlyList<AgentExecutionStage> stages)
		{
			ForcedAgentMarker? marker = null;
			for (int i = chat.Messages.Count - 1; i >= 0; i--)
			{
				var message = chat.Messages[i].Message;
				if (message is UserMessage)
				{
					if (message.AdditionalData.TryGet<ForcedAgentMarker>(out var found))
					{
						marker = found;
						break;
					}

					continue;
				}

				break;
			}

			if (marker is null)
				return null;

			if (services.GetRequiredService<IAgentManagementService>().TryGetAgentDescriptor(marker.AgentId) is null)
				return null;

			var stageId = stages.FirstOrDefault(s => s.Enabled && ContainsAgent(s, marker.AgentId))?.Id
				?? stages.FirstOrDefault(s => ContainsAgent(s, marker.AgentId))?.Id
				?? Guid.Empty;

			return (marker.AgentId, stageId);
		}

		private static bool ContainsAgent(AgentExecutionStage stage, Guid agentId)
			=> stage.AgentInstances.Any(instance => instance.AgentId == agentId);

		private (IReadOnlyList<UserMessage> UserMessages, IReadOnlyList<AssistantMessage> AssistantMessages) CollectThisRoundMessages()
		{
			var userMessages = new List<UserMessage>();
			var assistantMessages = new List<AssistantMessage>();

			bool foundUser = false;

			for (int i = chat.Messages.Count - 1; i >= 0; i--)
			{
				var message = chat.Messages[i].Message;

				if (message is AssistantMessage asm)
				{
					if (foundUser)
						break;
					assistantMessages.Add(asm);
				}
				else if (message is UserMessage usm)
				{
					userMessages.Add(usm);
					foundUser = true;
				}
				else
				{
					break;
				}
			}

			userMessages.Reverse();
			assistantMessages.Reverse();

			return (userMessages.AsReadOnly(), assistantMessages.AsReadOnly());
		}

		private IReadOnlyList<(Guid AgentId, Guid StageId)> CollectExecutedAgents(IReadOnlyList<AssistantMessage> assistantMessages)
		{
			var executedInThisRound = new List<(Guid AgentId, Guid StageId)>();

			Guid? prevAgentId = null, prevStageId = null;
			foreach (var msg in assistantMessages)
			{
				if (msg.SenderAgentId == prevAgentId && msg.AgentStageId == prevStageId)
					continue;

				executedInThisRound.Add((msg.SenderAgentId, msg.AgentStageId));

				prevAgentId = msg.SenderAgentId;
				prevStageId = msg.AgentStageId;
			}

			return executedInThisRound.AsReadOnly();
		}

		/// <summary>
		/// Finds the previous execution to resume from: the nearest trailing assistant message that belongs to a stage
		/// which still exists.
		/// </summary>
		/// <remarks>
		/// A stage can be removed from the chat's settings (or a stage-less forced turn can tag its message with
		/// <see cref="Guid.Empty"/>) while its messages stay in the history. Such messages are skipped, so that the
		/// returned <c>prevStageId</c> is always a valid stage, and the trailing run of messages from the same agent
		/// around the found message becomes <c>PreviousAgentMessages</c>. When nothing valid is found in the round the
		/// result is empty and the caller restarts from the first stage.
		/// </remarks>
		private (Guid? AgentId, Guid? StageId, IReadOnlyList<AssistantMessage> Messages) GetPreviousExecutionInfo(
			IReadOnlyList<AssistantMessage> thisRoundMessages, IReadOnlyList<AgentExecutionStage> stages)
		{
			int previousIndex = -1;
			for (int i = thisRoundMessages.Count - 1; i >= 0; i--)
			{
				if (stages.Any(stage => stage.Id == thisRoundMessages[i].AgentStageId))
				{
					previousIndex = i;
					break;
				}
			}

			if (previousIndex < 0)
				return (null, null, []);

			var asm = thisRoundMessages[previousIndex];
			var agentMessages = new List<AssistantMessage> { asm };

			for (int j = previousIndex - 1; j >= 0; j--)
			{
				if (thisRoundMessages[j].SenderAgentId == asm.SenderAgentId)
					agentMessages.Add(thisRoundMessages[j]);
				else
					break;
			}

			agentMessages.Reverse();
			return (asm.SenderAgentId, asm.AgentStageId, agentMessages.AsReadOnly());
		}

		private int FindTargetStageIndex(Guid? previousStageId)
		{
			if (previousStageId == null)
				return -1;

			var stages = chatSettings.Settings.Agents.ExecutionStages;

			for (int i = 0; i < stages.Count; i++)
			{
				if (stages[i].Id == previousStageId && stages[i].Enabled)
					return i;
			}

			return -1;
		}

		private (IReadOnlyList<Guid> AgentIds, IReadOnlyList<AssistantMessage> Messages) GetCurrentStageInfo(
			IReadOnlyList<AssistantMessage> thisRoundMessages, Guid? stageId)
		{
			var agentIds = new List<Guid>();
			var messages = new List<AssistantMessage>();
			Guid? lastAgentId = null;

			for (int i = thisRoundMessages.Count - 1; i >= 0; i--)
			{
				var message = thisRoundMessages[i];
				if (message.AgentStageId != stageId)
					break;

				messages.Add(message);
				if (lastAgentId != message.SenderAgentId)
				{
					lastAgentId = message.SenderAgentId;
					agentIds.Add(message.SenderAgentId);
				}
			}

			agentIds.Reverse();
			messages.Reverse();
			return (agentIds.AsReadOnly(), messages.AsReadOnly());
		}
	}
}
