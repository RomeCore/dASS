using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Tests.Storage;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the effective chat context walk: round window, visibility, checkpoints and their indices.
/// </summary>
[Collection("Prompting")]
public class AgentEffectiveMessagesProviderTests
{
	private static AgentEffectiveMessagesProvider CreateProvider(Chat chat, out FakeMessageVisibilityService visibility)
	{
		visibility = new FakeMessageVisibilityService();
		return new AgentEffectiveMessagesProvider(chat, new FakeChatSettingsService(), visibility);
	}

	[Fact]
	public void NoCuts_ReturnsAllMessages_AndNoCheckpoints()
	{
		var chat = PromptingTestHelpers.CreateChat(
			PromptingTestHelpers.User("u0", 0),
			PromptingTestHelpers.Assistant("a1", 1),
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(4, effective.Messages.Count);
		Assert.Equal(["u0", "a1", "u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		Assert.Empty(effective.Checkpoints);
		Assert.Equal(-1, effective.LastCutIndex);
	}

	[Fact]
	public void RoundWindow_AppliedInDynamicMode()
	{
		var chat = PromptingTestHelpers.CreateChat(
			PromptingTestHelpers.User("u0", 0),
			PromptingTestHelpers.Assistant("a1", 1),
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.MaxVisibleRounds = 1;

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
	}

	[Fact]
	public void RoundWindow_NotAppliedInHybridMode()
	{
		var chat = PromptingTestHelpers.CreateChat(
			PromptingTestHelpers.User("u0", 0),
			PromptingTestHelpers.Assistant("a1", 1),
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.MaxVisibleRounds = 1;
		agent.Context.PromptMode = PromptContextMode.Hybrid;

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(4, effective.Messages.Count);
	}

	[Fact]
	public void InvisibleUserMessage_Excluded()
	{
		var chat = PromptingTestHelpers.CreateChat(
			PromptingTestHelpers.User("u0", 0),
			PromptingTestHelpers.Assistant("a1", 1),
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var provider = CreateProvider(chat, out var visibility);
		visibility.IsUserVisible = m => m.Message.Content != "u0";
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["a1", "u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content));
	}

	[Fact]
	public void ShieldCheckpoint_CutsOlderMessages_AndIsCarriedAtMinusOne()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var shield = PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.Shield);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(shield, carried.Checkpoint);
		Assert.Equal(-1, carried.Index);
		Assert.Equal(-1, effective.LastCutIndex);
	}

	[Fact]
	public void ShieldCheckpoint_DisabledByAgentFlags_IsIgnored()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.Shield);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.DisabledFlags = ContextCheckpointKind.Shield;

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(4, effective.Messages.Count);
		Assert.Empty(effective.Checkpoints);
		Assert.Equal(-1, effective.LastCutIndex);
	}

	[Fact]
	public void SummaryCheckpoint_BreaksAfterEncounteredUserMessage()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var summary = PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.Summary);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(summary, carried.Checkpoint);
		Assert.Equal(-1, carried.Index);
		Assert.Equal(-1, effective.LastCutIndex);
	}

	[Fact]
	public void SummaryCheckpoint_KeepsBoundaryUserMessage_WhenNoUserSeenYet()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var a2 = PromptingTestHelpers.Assistant("a2", 2); // pending response
		var chat = PromptingTestHelpers.CreateChat(u0, a1, a2);
		var summary = PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.Summary);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u0", "a2"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(summary, carried.Checkpoint);
		// The cut carrier (a1) is invisible to the agent, so the checkpoint borrows the index
		// of the nearest preceding visible message (u0) — the same rule as for non-cut checkpoints.
		Assert.Equal(0, carried.Index);
		Assert.Equal(0, effective.LastCutIndex); // ...and the cut boundary accounts for the kept boundary message
	}

	[Fact]
	public void SummaryCheckpoint_OnInvisibleCarrier_IsImmuneToVisibility()
	{
		var uOld = PromptingTestHelpers.User("uOld", 0);
		var aOld = PromptingTestHelpers.Assistant("aOld", 1);
		var u1 = PromptingTestHelpers.User("u1", 2);
		var chat = PromptingTestHelpers.CreateChat(uOld, aOld, u1,
			PromptingTestHelpers.Assistant("a2", 3),
			PromptingTestHelpers.User("u2", 4),
			PromptingTestHelpers.Assistant("a3", 5));
		var summary = PromptingTestHelpers.AddCheckpoint(u1, ContextCheckpointKind.Summary);
		var provider = CreateProvider(chat, out var visibility);
		visibility.IsUserVisible = m => m.Message.Content != "u1";
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		// The summary is applied even though its carrier is invisible: everything older than u1 is cut.
		Assert.Equal(["a2", "u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(summary, carried.Checkpoint);
	}

	[Fact]
	public void NonCutCheckpoint_OnVisibleCarrier_UsesCarrierIndex()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var u1 = PromptingTestHelpers.User("u1", 1);
		var a2 = PromptingTestHelpers.Assistant("a2", 2);
		var chat = PromptingTestHelpers.CreateChat(u0, u1, a2,
			PromptingTestHelpers.User("u3", 3),
			PromptingTestHelpers.Assistant("a4", 4));
		var compaction = PromptingTestHelpers.AddCheckpoint(a2, ContextCheckpointKind.ToolCompaction);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(5, effective.Messages.Count);
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(compaction, carried.Checkpoint);
		Assert.Equal(2, carried.Index); // a2 is the third message of the effective set
	}

	[Fact]
	public void NonCutCheckpoint_OnInvisibleCarrier_UsesNearestPrecedingVisibleIndex()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var u1 = PromptingTestHelpers.User("u1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, u1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var compaction = PromptingTestHelpers.AddCheckpoint(u1, ContextCheckpointKind.ToolCompaction);
		var provider = CreateProvider(chat, out var visibility);
		visibility.IsUserVisible = m => m.Message.Content != "u1";
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u0", "u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(compaction, carried.Checkpoint);
		Assert.Equal(0, carried.Index); // u1 is invisible; the nearest preceding visible message is u0
	}

	[Fact]
	public void DisabledSummary_NotCompleted_IsIgnored()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.Summary, enabled: false);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(4, effective.Messages.Count);
		Assert.Empty(effective.Checkpoints);
	}

	[Fact]
	public void CombinedKinds_CutAndCompaction_AreProcessedTogether()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var checkpoint = PromptingTestHelpers.AddCheckpoint(a1,
			ContextCheckpointKind.Shield | ContextCheckpointKind.ToolCompaction);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.Equal(["u2", "a3"], effective.Messages.Select(m => m.BranchedMessage.Message.Content).ToArray());
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(checkpoint, carried.Checkpoint);
		Assert.Equal(-1, effective.LastCutIndex);
	}

	[Fact]
	public void DisabledFlags_MaskOutPartsOfCombinedKinds()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, a1,
			PromptingTestHelpers.User("u2", 2),
			PromptingTestHelpers.Assistant("a3", 3));
		var checkpoint = PromptingTestHelpers.AddCheckpoint(a1,
			ContextCheckpointKind.ToolCompaction | ContextCheckpointKind.ReasoningCompaction);
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.DisabledFlags = ContextCheckpointKind.ToolCompaction;

		var effective = provider.GetEffectiveMessages(agent);

		// The reasoning bit is still active: the checkpoint is carried with its carrier index.
		var carried = Assert.Single(effective.Checkpoints);
		Assert.Same(checkpoint, carried.Checkpoint);
		Assert.Equal(1, carried.Index);

		// Fully disabled: the checkpoint is inert.
		agent.Context.DisabledFlags = ContextCheckpointKind.ToolCompaction | ContextCheckpointKind.ReasoningCompaction;
		effective = provider.GetEffectiveMessages(agent);
		Assert.Empty(effective.Checkpoints);
	}
}
