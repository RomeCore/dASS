using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Providers.SubAgents;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the sub-agents section delta (built on the common addon delta engine):
/// description changes.
/// </summary>
[Collection("Prompting")]
public class SubAgentsDeltaProviderTests
{
	private sealed class FakeStateProvider : IPromptSectionStateProvider<SubAgentsSectionState>
	{
		public SubAgentsSectionState? State { get; init; }

		public SubAgentsSectionState? CaptureState(ChatAgentDescriptor agent) => State;
	}

	private static SubAgentsDeltaProvider CreateProvider(SubAgentsSectionState? state)
		=> new(new FakeStateProvider { State = state });

	private static EffectiveChatContext CreateContext() => new()
	{
		Agent = new ChatAgentDescriptor(),
		Messages = [],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1
	};

	private static SubAgentItem SubAgent(string name, string description = "description") => new()
	{
		Name = name,
		Description = description
	};

	private static SubAgentsSectionState State(params SubAgentItem[] subAgents)
		=> new() { Items = [.. subAgents] };

	[Fact]
	public void NothingChanged_ReturnsNull()
	{
		var provider = CreateProvider(State(SubAgent("web-searcher")));

		var delta = provider.CalculateDelta(State(SubAgent("web-searcher")), [], CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void ChangedDescription_IsReported()
	{
		var provider = CreateProvider(State(SubAgent("web-searcher", "new description")));

		var delta = provider.CalculateDelta(State(SubAgent("web-searcher", "old description")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.DescriptionChanged);
		Assert.Equal("new description", updated.Changes.NewDescription);
	}

	[Fact]
	public void NewSubAgent_CarriesFullDefinition()
	{
		var provider = CreateProvider(State(SubAgent("web-searcher")));

		var delta = provider.CalculateDelta(null, [], CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("web-searcher", added.Name);
		Assert.NotNull(added.Definition);
		Assert.Equal("description", added.Definition!.Description);
	}
}
