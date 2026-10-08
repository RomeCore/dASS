using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using RCMessages = RCLargeLanguageModels.Messages;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// End-to-end composition tests for <see cref="AgentPromptComposer"/>: the header source per prompt mode,
/// the summary checkpoint, SCM delta/stamp announcements, live tails and the tool set.
/// </summary>
[Collection("Prompting")]
public class AgentPromptComposerTests
{
	private static ChatAgentDescriptor HybridAgent()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.PromptMode = PromptContextMode.Hybrid;
		return agent;
	}

	private static string[] Texts(AgentPromptBundle bundle)
		=> [.. bundle.Messages.Select(m => m.Content ?? string.Empty)];

	// =====================================================
	// === Header modes                                  ===
	// =====================================================

	[Fact]
	public void DynamicMode_RendersTheLiveHeaderFromTheSections()
	{
		var agent = PromptingTestHelpers.CreateAgent(); // Dynamic by default
		var harness = new PromptComposerHarness(
				PromptingTestHelpers.User("u0", 0),
				PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id))
			.WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.IsType<RCMessages.SystemMessage>(bundle.Messages[0]);
		Assert.Equal("core", bundle.Messages[0].Content);
		Assert.Contains("QUOTE(u0)", Texts(bundle));
	}

	[Fact]
	public void HybridMode_FreezesTheHeaderFromANewAnchor_AndReusesIt()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		Assert.Empty(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		var first = composer.Build(agent);

		// The anchor was created once (append-only) and reused on the second build.
		var anchor1 = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		var second = composer.Build(agent);

		Assert.Equal("core", first.Messages[0].Content);
		Assert.Equal("core", second.Messages[0].Content);

		var anchor2 = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.Same(anchor1, anchor2); // Same anchor instance reused.
	}

	[Fact]
	public void StaticMode_FreezesTheSnapshot_AndReusesIt()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.PromptMode = PromptContextMode.Static;
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		var bundle = composer.Build(agent);

		Assert.Equal("core", bundle.Messages[0].Content);
		Assert.NotNull(agent.Context.Snapshot);
		Assert.Equal("core", agent.Context.Snapshot!.Text);
	}

	// =====================================================
	// === Summary                                      ===
	// =====================================================

	[Fact]
	public void SummaryCheckpoint_IsInjectedAsASummaryUserMessage()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1, agent.Id);
		var u2 = PromptingTestHelpers.User("u2", 2);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, u2, pending).WithSection(new FakeSection(0, "core"));
		var checkpoint = PromptingTestHelpers.AddCheckpoint(u2, ContextCheckpointKind.Summary);
		checkpoint.Context = "SUM";

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Contains(Texts(bundle), t => t.Contains("<summary>") && t.Contains("SUM"));
		Assert.DoesNotContain(Texts(bundle), t => t.Contains("u0"));
		Assert.DoesNotContain(Texts(bundle), t => t.Contains("a1"));
	}

	// =====================================================
	// === SCM deltas and stamps                         ===
	// =====================================================

	[Fact]
	public void DeltaMatchingTheLiveAnchor_IsAnnouncedInTheSystemReminder()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent); // creates the anchor
		var anchor = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		a1.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = anchor.Id, Snapshot = "DELTA" });

		var bundle = composer.Build(agent);

		Assert.Contains(Texts(bundle), t => t.Contains("DELTA"));
	}

	[Fact]
	public void SupersedeStamp_IsAnnouncedInTheSystemReminder()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var a2 = PromptingTestHelpers.Completed("a2", 2, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, agent.Id);
		a2.Message.AdditionalData.Add(new PromptSupersedeStampMessageData
		{
			Stamps = [new PromptSupersedeStampBase { Discriminator = "d", Snapshot = "STAMP" }]
		});
		var harness = new PromptComposerHarness(u0, a1, a2, pending).WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Contains(Texts(bundle), t => t.Contains("STAMP"));
	}

	[Fact]
	public void DeltaOfAHiddenCarrier_IsStillAnnounced()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var hidden = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, hidden, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent); // creates the anchor
		var anchor = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		hidden.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = anchor.Id, Snapshot = "HIDDEN-DELTA" });
		hidden.Message.IsDisabledForAgents = true; // hidden from the agent, but its announcement must survive

		var bundle = composer.Build(agent);

		Assert.Contains(Texts(bundle), t => t.Contains("HIDDEN-DELTA"));
	}

	// =====================================================
	// === Live tails and tools                          ===
	// =====================================================

	[Fact]
	public void LiveTail_IsInjectedIntoThePendingAssistantReminder()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending)
			.WithSection(new FakeSection(0, "core"))
			.WithLiveTail("LIVE");

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Contains(Texts(bundle), t => t.Contains("LIVE"));
	}

	[Fact]
	public void ToolDefinitionsFromTheHeader_AreExposedInTheBundle()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "core", PromptingTestHelpers.Tool("my-tool")));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Contains(bundle.Tools, t => t.Name == "my-tool");
	}

	[Fact]
	public void Build_InvalidatesTheToolsetCache()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0));

		harness.CreateComposer().Build(agent);

		Assert.Equal(1, harness.ToolsetCache.InvalidateCount);
	}
}
