using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Hooks;
using RCMessages = RCLargeLanguageModels.Messages;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Boundary and negative composition tests for <see cref="AgentPromptComposer"/>: header rebaselining and hidden
/// carriers, summary gating, mismatched/foreign SCM data, hidden stamps, live-tail gating, hooks, tools, dumping
/// and byte stability.
/// </summary>
[Collection("Prompting")]
public class AgentPromptComposerEdgeTests
{
	private static ChatAgentDescriptor DynamicAgent() => PromptingTestHelpers.CreateAgent();

	private static ChatAgentDescriptor HybridAgent()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.PromptMode = PromptContextMode.Hybrid;
		return agent;
	}

	private static ChatAgentDescriptor StaticAgent()
	{
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.PromptMode = PromptContextMode.Static;
		return agent;
	}

	private static string[] Texts(AgentPromptBundle bundle)
		=> [.. bundle.Messages.Select(m => m.Content ?? string.Empty)];

	private static bool Has(AgentPromptBundle bundle, string needle)
		=> Texts(bundle).Any(t => t.Contains(needle));

	// =====================================================
	// === Header: modes, ordering, rebaseline, hidden   ===
	// =====================================================

	[Fact]
	public void DynamicMode_RebuildsTheHeaderEveryTime()
	{
		var agent = DynamicAgent();
		var section = new MutableSection { Text = "v1" };
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0)).WithProvider(section);
		var composer = harness.CreateComposer();

		Assert.Equal("v1", composer.Build(agent).Messages[0].Content);

		section.Text = "v2";

		Assert.Equal("v2", composer.Build(agent).Messages[0].Content);
	}

	[Fact]
	public void DynamicMode_WithNoSections_HasAnEmptyHeader()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Equal(string.Empty, bundle.Messages[0].Content);
	}

	[Fact]
	public void Header_JoinsMultipleSectionsInCollectionOrder()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "first"))
			.WithSection(new FakeSection(1, "second"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Equal("first\nsecond", bundle.Messages[0].Content);
	}

	[Fact]
	public void StaticMode_FreezesTheSnapshot_EvenWhenTheSectionsChange()
	{
		var agent = StaticAgent();
		var section = new MutableSection { Text = "v1" };
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0)).WithProvider(section);
		var composer = harness.CreateComposer();

		composer.Build(agent);
		var frozen = agent.Context.Snapshot;
		section.Text = "v2";
		var bundle = composer.Build(agent);

		Assert.NotNull(frozen);
		Assert.Equal("v1", bundle.Messages[0].Content);
		Assert.Same(frozen, agent.Context.Snapshot); // the snapshot is captured once
	}

	[Fact]
	public void HybridMode_InstallsTheAnchorOnTheFirstRawMessage()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));

		harness.CreateComposer().Build(agent);

		Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.Empty(a1.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
	}

	[Fact]
	public void HybridMode_RebaselinesOnTheFirstRawMessageAfterANewCut()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent);
		var first = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());

		// A cut on the anchor's own message invalidates it: the anchor is rebaselined on the next raw message.
		PromptingTestHelpers.AddCheckpoint(u0, ContextCheckpointKind.Shield);
		composer.Build(agent);

		var second = Assert.Single(a1.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.NotSame(first, second);
		Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>()); // the old anchor stays inert
	}

	[Fact]
	public void HybridMode_ReusesAnAnchorCarriedByAHiddenMessage()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent);
		u0.Message.IsDisabledForAgents = true; // its carrier is hidden from the agent now

		var bundle = composer.Build(agent);

		// Ticket 19: the anchor survives on the raw history — no rebaseline, header still from the anchor snapshot.
		Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.Empty(a1.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		Assert.Equal("core", bundle.Messages[0].Content);
		Assert.False(Has(bundle, "QUOTE(u0)"));
	}

	// =====================================================
	// === Summary                                      ===
	// =====================================================

	[Fact]
	public void Messages_AreOrdered_SystemSummaryConversation()
	{
		var agent = DynamicAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithSection(new FakeSection(0, "core"));
		PromptingTestHelpers.AddCheckpoint(u0, ContextCheckpointKind.Summary).Context = "SUM";

		var bundle = harness.CreateComposer().Build(agent);

		Assert.IsType<RCMessages.SystemMessage>(bundle.Messages[0]);
		Assert.Contains("SUM", bundle.Messages[1].Content!);
		Assert.Contains("QUOTE(u0)", bundle.Messages[2].Content!);
	}

	[Fact]
	public void SummaryCheckpoint_RespectsTheDisabledFlags()
	{
		var agent = DynamicAgent();
		agent.Context.DisabledFlags = ContextCheckpointKind.Summary;
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithSection(new FakeSection(0, "core"));
		PromptingTestHelpers.AddCheckpoint(u0, ContextCheckpointKind.Summary).Context = "SUM";

		var bundle = harness.CreateComposer().Build(agent);

		Assert.False(Has(bundle, "<summary>"));
	}

	[Fact]
	public void NoSummaryCheckpoint_MeansNoSummaryMessage()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0)).WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.False(Has(bundle, "<summary>"));
	}

	// =====================================================
	// === SCM deltas and stamps                         ===
	// =====================================================

	[Fact]
	public void DeltaWithAMismatchedAnchorId_IsIgnored()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent);
		var anchor = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		a1.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = anchor.Id + 999, Snapshot = "WRONG" });

		var bundle = composer.Build(agent);

		Assert.False(Has(bundle, "WRONG"));
	}

	[Fact]
	public void DeltaProducedByASection_IsAnnounced()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var section = new MutableSection();
		var harness = new PromptComposerHarness(u0, pending).WithProvider(section);
		var composer = harness.CreateComposer();

		composer.Build(agent); // creates the anchor
		section.Text = "v2";
		section.EmitDelta = true;

		var bundle = composer.Build(agent);

		Assert.True(Has(bundle, "DELTA(v2)"));
	}

	[Fact]
	public void MultipleStampsOnOneMessage_AreAllAnnounced()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var a2 = PromptingTestHelpers.Completed("a2", 2, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, agent.Id);
		a2.Message.AdditionalData.Add(new PromptSupersedeStampMessageData
		{
			Stamps =
			[
				new PromptSupersedeStampBase { Discriminator = "a", Snapshot = "STAMP-A" },
				new PromptSupersedeStampBase { Discriminator = "b", Snapshot = "STAMP-B" }
			]
		});
		var harness = new PromptComposerHarness(u0, a1, a2, pending).WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.True(Has(bundle, "STAMP-A"));
		Assert.True(Has(bundle, "STAMP-B"));
	}

	[Fact]
	public void StampOnAHiddenCarrier_IsStillAnnounced()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var hidden = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		hidden.Message.AdditionalData.Add(new PromptSupersedeStampMessageData
		{
			Stamps = [new PromptSupersedeStampBase { Discriminator = "d", Snapshot = "HIDDEN-STAMP" }]
		});
		hidden.Message.IsDisabledForAgents = true;
		var harness = new PromptComposerHarness(u0, hidden, pending).WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.True(Has(bundle, "HIDDEN-STAMP"));
	}

	[Fact]
	public void HiddenDelta_IsHostedByThePendingTurnWhenNoAssistantFollows()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var visible = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var hidden = PromptingTestHelpers.Completed("a2", 2, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 3, agent.Id);
		var harness = new PromptComposerHarness(u0, visible, hidden, pending).WithSection(new FakeSection(0, "core"));
		var composer = harness.CreateComposer();

		composer.Build(agent); // creates the anchor
		var anchor = Assert.Single(u0.Message.AdditionalData.GetAll<PromptStateAnchorMessageData>());
		hidden.Message.AdditionalData.Add(new PromptStateDeltaMessageData { AnchorId = anchor.Id, Snapshot = "HOSTED" });
		hidden.Message.IsDisabledForAgents = true;

		var bundle = composer.Build(agent);

		Assert.True(Has(bundle, "HOSTED"));
	}

	// =====================================================
	// === Live tails                                    ===
	// =====================================================

	[Fact]
	public void MultipleLiveTails_AreAllInjected()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithLiveTail("LIVE-A").WithLiveTail("LIVE-B");

		var bundle = harness.CreateComposer().Build(agent);

		Assert.True(Has(bundle, "LIVE-A"));
		Assert.True(Has(bundle, "LIVE-B"));
	}

	[Fact]
	public void LiveTail_IsNotInjectedWhenTheLastAssistantIsCompleted()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(
				PromptingTestHelpers.User("u0", 0),
				PromptingTestHelpers.Completed("a1", 1, agent.Id))
			.WithLiveTail("LIVE");

		var bundle = harness.CreateComposer().Build(agent);

		Assert.False(Has(bundle, "LIVE"));
	}

	[Fact]
	public void WhitespaceLiveTail_ProducesNoReminder()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithLiveTail("   ");

		var bundle = harness.CreateComposer().Build(agent);

		Assert.False(Has(bundle, "<system-reminder>"));
	}

	// =====================================================
	// === Hooks                                         ===
	// =====================================================

	[Fact]
	public void PromptBuildingHook_CanReplaceAMessage()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "core"))
			.WithHook(new FakeHook(0, (_, message) =>
				message.Message.Content == "u0" ? [new RCMessages.UserMessage("HOOKED")] : null));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.True(Has(bundle, "HOOKED"));
		Assert.False(Has(bundle, "QUOTE(u0)"));
	}

	[Fact]
	public void PromptBuildingHooks_AreAppliedInOrder()
	{
		var agent = DynamicAgent();
		var calls = new List<string>();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "core"))
			.WithHook(new FakeHook(20, (_, _) => { calls.Add("B"); return null; }))
			.WithHook(new FakeHook(10, (_, _) => { calls.Add("A"); return null; }));

		harness.CreateComposer().Build(agent);

		Assert.Equal(["A", "B"], calls.Take(2));
	}

	[Fact]
	public void PromptBuildingHook_ReturningNull_LeavesTheContextUnchanged()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "core"))
			.WithHook(new FakeHook(0, (_, _) => null));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.True(Has(bundle, "QUOTE(u0)"));
	}

	// =====================================================
	// === Tools, cache, dump, stability, empty chat     ===
	// =====================================================

	[Fact]
	public void ToolsFromMultipleSections_AreMergedAndSorted()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0))
			.WithSection(new FakeSection(0, "a", PromptingTestHelpers.Tool("z")))
			.WithSection(new FakeSection(1, "b", PromptingTestHelpers.Tool("a")));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Equal(["a", "z"], bundle.Tools.Select(t => t.Name).ToArray());
	}

	[Fact]
	public void Build_InvalidatesTheCacheOnEveryCall()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0));
		var composer = harness.CreateComposer();

		composer.Build(agent);
		composer.Build(agent);

		Assert.Equal(2, harness.ToolsetCache.InvalidateCount);
	}

	[Fact]
	public void Dump_ReportsTheAnchorHeaderSource_WhenEnabled()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 1, agent.Id);
		var harness = new PromptComposerHarness(u0, pending).WithSection(new FakeSection(0, "core"));

		PromptDumpService.IsEnabled = true;
		try
		{
			harness.CreateComposer().Build(agent);
		}
		finally
		{
			PromptDumpService.IsEnabled = false;
		}

		var dump = Assert.Single(harness.Dump.Dumps);
		Assert.Contains("mode=Hybrid", dump.Context!);
		Assert.Contains("header=anchor#", dump.Context!);
	}

	[Fact]
	public void Dump_ReportsTheLiveHeaderSource_WhenEnabled()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness(PromptingTestHelpers.User("u0", 0)).WithSection(new FakeSection(0, "core"));

		PromptDumpService.IsEnabled = true;
		try
		{
			harness.CreateComposer().Build(agent);
		}
		finally
		{
			PromptDumpService.IsEnabled = false;
		}

		var dump = Assert.Single(harness.Dump.Dumps);
		Assert.Contains("mode=Dynamic", dump.Context!);
		Assert.Contains("header=live", dump.Context!);
	}

	[Fact]
	public void TwoBuildsWithoutChanges_AreByteIdentical()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending)
			.WithSection(new FakeSection(0, "core", PromptingTestHelpers.Tool("t")));
		var composer = harness.CreateComposer();

		var first = composer.Build(agent);
		var second = composer.Build(agent);

		Assert.Equal(Texts(first), Texts(second));
		Assert.Equal(first.Tools.Select(t => t.Name), second.Tools.Select(t => t.Name));
	}

	[Fact]
	public void EmptyChat_YieldsOnlyTheSystemMessage()
	{
		var agent = DynamicAgent();
		var harness = new PromptComposerHarness().WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Single(bundle.Messages);
		Assert.Equal("core", bundle.Messages[0].Content);
	}

	[Fact]
	public void SummaryAndConversation_MatchTheFullStructure()
	{
		var agent = HybridAgent();
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Completed("a1", 1, agent.Id);
		var pending = PromptingTestHelpers.Assistant(string.Empty, 2, agent.Id);
		var harness = new PromptComposerHarness(u0, a1, pending).WithSection(new FakeSection(0, "core"));

		var bundle = harness.CreateComposer().Build(agent);

		Assert.Equal("core", bundle.Messages[0].Content);
		Assert.Contains("QUOTE(u0)", Texts(bundle));
		Assert.Contains("a1", Texts(bundle)); // the completed assistant turn is quoted/kept
	}

	// =====================================================
	// === Fakes                                        ===
	// =====================================================

	private sealed class MutableSection : IPromptAnchoredSectionProvider
	{
		public string Discriminator { get; } = "mutable";

		public string Text { get; set; } = "v1";

		public bool EmitDelta { get; set; }

		public PromptSectionStateBase CaptureState(ChatAgentDescriptor agent) => new FakeSectionState();

		public SystemPromptSnapshot RenderState(PromptSectionStateBase state) => new() { Text = Text, Tools = [] };

		public PromptSectionDeltaBase? CalculateDelta(PromptSectionStateBase? anchorState,
			IEnumerable<PromptSectionDeltaBase> existingDeltas, EffectiveChatContext context)
			=> EmitDelta && !existingDeltas.Any() ? new FakeSectionDelta() : null;

		public string RenderDelta(PromptSectionDeltaBase delta) => $"DELTA({Text})";
	}

	private sealed class FakeHook(int order,
		Func<IEnumerable<RCMessages.IMessage>, BranchedMessage, IEnumerable<RCMessages.IMessage>?> transform)
		: IPromptBuildingHook
	{
		public int Order => order;

		public IEnumerable<RCMessages.IMessage>? ModifyFinalContext(IEnumerable<RCMessages.IMessage> messages,
			BranchedMessage message, ChatAgentDescriptor agent) => transform(messages, message);
	}
}
