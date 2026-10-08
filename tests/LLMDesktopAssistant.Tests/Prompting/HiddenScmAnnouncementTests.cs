using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// The SCM decoupling from visibility: announcements (anchor deltas and supersede stamps) carried by a message
/// hidden from the agent must still be surfaced, hosted by the nearest visible assistant message at or after
/// the carrier.
/// </summary>
[Collection("Prompting")]
public class HiddenScmAnnouncementTests
{
	private static EffectiveChatContext Effective(params BranchedMessage[] messages) => new()
	{
		Agent = new ChatAgentDescriptor(),
		Messages = [.. messages.Select(ToEffective)],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1,
	};

	private static EffectiveMessage ToEffective(BranchedMessage message)
		=> new(message, MessagePartsFacet.All, ContextCheckpointKind.None, MessageAuthorIdentity.Default);

	private static PromptStateAnchorMessageData Anchor(Guid agentId) => new()
	{
		Id = 1,
		AgentId = agentId,
		Snapshot = new SystemPromptSnapshot { Text = "anchor" }
	};

	[Fact]
	public void HiddenCarrier_IsHostedByTheNextVisibleAssistant()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var hidden = PromptingTestHelpers.Assistant("a1", 1, senderAgentId: agent.Id);
		var visible = PromptingTestHelpers.Assistant("a2", 2, senderAgentId: agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, senderAgentId: agent.Id);
		var chat = PromptingTestHelpers.CreateChat(u0, hidden, visible, pending);

		hidden.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = 1, Snapshot = "delta-1" });
		hidden.Message.AdditionalData.Add(new PromptSupersedeStampMessageData
		{
			Stamps = [new PromptSupersedeStampBase { Snapshot = "stamp-1" }]
		});

		// 'hidden' is not in the effective set.
		var effective = Effective(u0, visible, pending);

		var map = AgentPromptComposer.CollectHiddenAnnouncements(chat.Messages, effective, agent.Id, Anchor(agent.Id), 0);

		var hosted = Assert.Single(map);
		Assert.Equal(1, hosted.Key); // 'visible' is the effective index 1
		Assert.Equal(["delta-1", "stamp-1"], hosted.Value);
	}

	[Fact]
	public void HiddenCarrier_WithNoAssistantAfter_FallsBackToThePendingTurn()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var visible = PromptingTestHelpers.Assistant("a1", 1, senderAgentId: agent.Id);
		var hidden = PromptingTestHelpers.Assistant("a2", 2, senderAgentId: agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, senderAgentId: agent.Id);
		var chat = PromptingTestHelpers.CreateChat(u0, visible, hidden, pending);

		hidden.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = 1, Snapshot = "delta-1" });

		var effective = Effective(u0, visible, pending);

		var map = AgentPromptComposer.CollectHiddenAnnouncements(chat.Messages, effective, agent.Id, Anchor(agent.Id), 0);

		var hosted = Assert.Single(map);
		Assert.Equal(2, hosted.Key); // the pending turn is the only visible assistant at or after the carrier
		Assert.Equal(["delta-1"], hosted.Value);
	}

	[Fact]
	public void VisibleCarrier_IsNotCollected()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var assistant = PromptingTestHelpers.Assistant("a1", 1, senderAgentId: agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, senderAgentId: agent.Id);
		var chat = PromptingTestHelpers.CreateChat(u0, assistant, pending);

		assistant.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = 1, Snapshot = "delta-1" });

		var effective = Effective(u0, assistant, pending);

		var map = AgentPromptComposer.CollectHiddenAnnouncements(chat.Messages, effective, agent.Id, Anchor(agent.Id), 0);

		// A visible carrier is rendered by the regular loop, not collected here.
		Assert.Empty(map);
	}

	[Fact]
	public void ForeignAgentCarrier_IsIgnored()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var foreign = PromptingTestHelpers.Assistant("a1", 1); // a different sender agent
		var visible = PromptingTestHelpers.Assistant("a2", 2, senderAgentId: agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, senderAgentId: agent.Id);
		var chat = PromptingTestHelpers.CreateChat(u0, foreign, visible, pending);

		foreign.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = 1, Snapshot = "delta-1" });

		var effective = Effective(u0, visible, pending);

		var map = AgentPromptComposer.CollectHiddenAnnouncements(chat.Messages, effective, agent.Id, Anchor(agent.Id), 0);

		Assert.Empty(map);
	}
}
