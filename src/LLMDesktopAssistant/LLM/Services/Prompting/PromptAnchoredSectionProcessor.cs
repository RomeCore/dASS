using System.Text;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Prompting.Context;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <inheritdoc cref="IPromptAnchoredSectionProcessor"/>
	[ChatService(typeof(IPromptAnchoredSectionProcessor))]
	public class PromptAnchoredSectionProcessor(
		Chat chat
	) : IPromptAnchoredSectionProcessor
	{
		/// <inheritdoc/>
		public PromptStateAnchorMessageData? Process(ChatAgentDescriptor agent,
			EffectiveChatContext effectiveContext, IEnumerable<IPromptAnchoredSectionProvider> sections)
		{
			var promptMode = agent.Context.PromptMode;
			if (promptMode != PromptContextMode.Hybrid)
				return null;

			if (effectiveContext.Messages.Count == 0 ||
				effectiveContext.Messages[^1].BranchedMessage.Message is not AssistantMessage { IsCompleted: false } pendingAssistantMessage)
				throw new InvalidOperationException("Expected a pending assistant message, but none was found.");

			PromptStateAnchorMessageData? anchor = null;
			int messageWithAnchor = -1;

			// Live anchor: the newest anchor of this agent positioned after the last checkpoint.
			// A single message may hold anchors of multiple agents — scan all of them.
			for (int i = effectiveContext.Messages.Count - 1; i > effectiveContext.LastCheckpointIndex; i--)
			{
				var branchedMessage = effectiveContext.Messages[i];
				foreach (var candidate in branchedMessage.BranchedMessage.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>())
				{
					if (candidate.AgentId != agent.Id)
						continue;

					Log.Debug("Reusing prompt state anchor #{AnchorId} for agent {AgentId} (message index {Index}).",
						candidate.Id, agent.Id, i);
					anchor = candidate;
					messageWithAnchor = i;
					break;
				}
			}

			if (anchor is not null)
			{
				if (messageWithAnchor + 1 < effectiveContext.Messages.Count)
				{
					var deltasPerAnchor = new Dictionary<string, List<PromptSectionDeltaBase>>();

					for (int i = messageWithAnchor + 1; i < effectiveContext.Messages.Count; i++)
					{
						var branchedMessage = effectiveContext.Messages[i];
						if (branchedMessage.BranchedMessage.Message is AssistantMessage assistantMessage && assistantMessage.SenderAgentId == agent.Id)
						{
							foreach (var deltaData in branchedMessage.BranchedMessage.Message.AdditionalData.OfType<PromptStateDeltaMessageData>())
							{
								if (deltaData.AnchorId != anchor.Id)
									continue;

								foreach (var delta in deltaData.Sections)
								{
									if (!deltasPerAnchor.TryGetValue(delta.Discriminator, out var deltaList))
										deltasPerAnchor.Add(delta.Discriminator, deltaList = []);
									deltaList.Add(delta);
								}
							}
						}
					}

					var deltas = new List<PromptSectionDeltaBase>();
					var sb = new StringBuilder();

					foreach (var section in sections)
					{
						var anchorState = anchor.Sections.FirstOrDefault(s => s.Discriminator == section.Discriminator);
						var existingDeltas = deltasPerAnchor.GetValueOrDefault(section.Discriminator) ?? [];

						var newDelta = section.CalculateDelta(anchorState, existingDeltas, effectiveContext);
						if (newDelta is not null)
						{
							newDelta.Discriminator = section.Discriminator;
							deltas.Add(newDelta);
							var rendered = section.RenderDelta(newDelta);
							sb.Append(rendered).Append('\n');
						}
					}

					if (deltas.Count > 0)
					{
						while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
							sb.Length--;

						pendingAssistantMessage.AdditionalData.Add(new PromptStateDeltaMessageData
						{
							AnchorId = anchor.Id,
							Sections = [.. deltas],
							Snapshot = sb.ToString()
						});
					}
				}

				return anchor;
			}

			// Rebaseline: create a new anchor on the first message after the last checkpoint.
			int targetIndex = effectiveContext.LastCheckpointIndex + 1;
			if (targetIndex >= effectiveContext.Messages.Count)
			{
				Log.Debug("Skipped prompt state anchor creation for agent {AgentId}: no target message after the last checkpoint.",
					agent.Id);
				return null;
			}

			var target = effectiveContext.Messages[targetIndex];
			var states = sections.CaptureStates(agent);
			var snapshot = sections.RenderHeader(states);

			anchor = new PromptStateAnchorMessageData
			{
				Id = GetNextAnchorId(),
				AgentId = agent.Id,
				Sections = [.. states],
				Snapshot = snapshot
			};

			target.BranchedMessage.Message.AdditionalData.Add(anchor);
			Log.Information("Created prompt state anchor #{AnchorId} for agent {AgentId} on message {MessageId} (effective index {Index}).",
				anchor.Id, agent.Id, target.BranchedMessage.MessageId, targetIndex);
			return anchor;
		}

		private int GetNextAnchorId()
		{
			if (!chat.AdditionalData.TryGet<PromptStateAnchorIdCounter>(out var counter))
			{
				counter = new PromptStateAnchorIdCounter();
				chat.AdditionalData.TryReplace(counter);
			}

			counter.LastId++;
			return counter.LastId;
		}
	}
}
