using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Tests.Storage;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the SCM stage: anchor lifecycle (create/reuse/rebaseline) and agent isolation.
/// </summary>
[Collection("Prompting")]
public class PromptStateStageTests
{
	/// <summary>
	/// Creates a stage. Sections are passed per <see cref="PromptAnchoredSectionProcessor.Process"/> call.
	/// </summary>
	private static PromptAnchoredSectionProcessor CreateStage(Chat chat) => new(chat, new FakeChatSettingsService());

	private static FakeSection[] CreateSections() => [new FakeSection(0, "core")];

	private static ChatAgentDescriptor CreateHybridAgent()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.PromptMode = PromptContextMode.Hybrid;
		return agent;
	}

	/// <summary>
	/// Creates an effective context of the given messages with no cuts and no checkpoints.
	/// </summary>
	private static EffectiveChatContext CreateEffectiveContext(params BranchedMessage[] messages)
		=> new()
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

	[Fact]
	public void CreatesAnchor_OnFirstHybridPrep()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1);
		var chat = PromptingTestHelpers.CreateChat(u0, pending);
		var stage = CreateStage(chat);

		var anchor = stage.Process(CreateHybridAgent(), CreateEffectiveContext(u0, pending), CreateSections());

		Assert.NotNull(anchor);
		Assert.Equal(1, anchor!.Id);
		Assert.Equal("core", anchor.Snapshot.Text);
		Assert.False(anchor.IsVisible);
		Assert.Single(anchor.Sections);
		var stored = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.Same(anchor, stored);
		Assert.True(chat.AdditionalData.TryGet<PromptStateAnchorIdCounter>(out var counter));
		Assert.Equal(1, counter.LastId);
	}

	[Fact]
	public void ReusesAnchor_OnSecondPrep()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1);
		var chat = PromptingTestHelpers.CreateChat(u0, pending);
		var stage = CreateStage(chat);
		var agent = CreateHybridAgent();
		var effective = CreateEffectiveContext(u0, pending);
		var sections = CreateSections();

		var first = stage.Process(agent, effective, sections);
		var second = stage.Process(agent, effective, sections);

		Assert.NotNull(first);
		Assert.Same(first, second);
		Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.True(chat.AdditionalData.TryGet<PromptStateAnchorIdCounter>(out var counter));
		Assert.Equal(1, counter.LastId);
	}

	[Fact]
	public void Rebaselines_WhenAnchorIsBeforeNewCut()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var u1 = PromptingTestHelpers.User("u1", 1);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2);
		var chat = PromptingTestHelpers.CreateChat(u0, u1, pending);
		var stage = CreateStage(chat);
		var agent = CreateHybridAgent();
		var sections = CreateSections();

		var first = stage.Process(agent, CreateEffectiveContext(u0, u1, pending), sections);
		Assert.NotNull(first);
		Assert.Equal(1, first!.Id);
		Assert.Same(first, Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>()));

		// The cut checkpoint is carried by the first message of the effective set (index 0),
		// so the anchor pinned to it is now before the cut and must be rebaselined.
		var cut = PromptingTestHelpers.AddCheckpoint(u0, ContextCheckpointKind.Shield);
		var effectiveAfterCut = new EffectiveChatContext
		{
			Agent = new ChatAgentDescriptor(),
			Messages = [.. new[] { u0, u1, pending }.Select(ToEffective)],
			Checkpoints = [new EffectiveCheckpoint(cut, 0)],
			EffectiveMessagesStartIndex = 0,
			LastCutIndex = 0,
			LastCheckpointIndex = 0,
		};

		var second = stage.Process(agent, effectiveAfterCut, sections);

		Assert.NotNull(second);
		Assert.Equal(2, second!.Id);
		Assert.Same(second, Assert.Single(u1.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>()));
		// The old anchor stays in history inert and untouched (append-only).
		Assert.Same(first, Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>()));
	}

	[Fact]
	public void AgentsDoNotShareAnchors()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1);
		var chat = PromptingTestHelpers.CreateChat(u0, pending);
		var stage = CreateStage(chat);
		var agentA = CreateHybridAgent();
		var agentB = CreateHybridAgent();
		var effective = CreateEffectiveContext(u0, pending);
		var sections = CreateSections();

		var anchorA = stage.Process(agentA, effective, sections);
		var anchorB = stage.Process(agentB, effective, sections);

		Assert.NotNull(anchorA);
		Assert.NotNull(anchorB);
		Assert.NotSame(anchorA, anchorB);
		Assert.Equal(agentA.Id, anchorA!.AgentId);
		Assert.Equal(agentB.Id, anchorB!.AgentId);
		var stored = u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>();
		Assert.Equal(2, stored.Count);
		Assert.Equal(2, anchorB.Id); // the id counter is chat-wide
	}

	[Fact]
	public void NonHybridMode_ReturnsNull_AndCreatesNothing()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1);
		var chat = PromptingTestHelpers.CreateChat(u0, pending);
		var stage = CreateStage(chat);

		var anchor = stage.Process(PromptingTestHelpers.CreateAgent(), CreateEffectiveContext(u0, pending), CreateSections());

		Assert.Null(anchor);
		Assert.Empty(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.False(chat.AdditionalData.TryGet<PromptStateAnchorIdCounter>(out _));
	}

	[Fact]
	public void NoPendingAssistantMessage_Throws()
	{
		var chat = PromptingTestHelpers.CreateChat();
		var stage = CreateStage(chat);

		Assert.Throws<InvalidOperationException>(() =>
			stage.Process(CreateHybridAgent(), CreateEffectiveContext(), CreateSections()));
	}

	[Fact]
	public void LastMessageIsNotPendingAssistant_Throws()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var chat = PromptingTestHelpers.CreateChat(u0);
		var stage = CreateStage(chat);

		Assert.Throws<InvalidOperationException>(() =>
			stage.Process(CreateHybridAgent(), CreateEffectiveContext(u0), CreateSections()));
	}
}
