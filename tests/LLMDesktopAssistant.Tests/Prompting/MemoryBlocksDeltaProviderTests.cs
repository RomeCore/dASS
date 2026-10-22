using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the memory blocks section delta (built on the common addon delta engine):
/// description, access and types changes.
/// </summary>
[Collection("Prompting")]
public class MemoryBlocksDeltaProviderTests
{
	private sealed class FakeStateProvider : IPromptSectionStateProvider<MemoryBlocksSectionState>
	{
		public MemoryBlocksSectionState? State { get; init; }

		public Task<MemoryBlocksSectionState?> CaptureStateAsync(ChatAgentDescriptor agent, CancellationToken cancellationToken = default) => Task.FromResult(State);
	}

	private static MemoryBlocksDeltaProvider CreateProvider(MemoryBlocksSectionState? state)
		=> new(new FakeStateProvider { State = state });

	private static EffectiveChatContext CreateContext() => new()
	{
		Chat = PromptingTestHelpers.CreateChat(),
		Services = new EmptyServiceProvider(),
		Agent = new ChatAgentDescriptor(),
		Messages = [],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1
	};

	private static MemoryBlockItem Block(string name,
		string description = "description",
		bool canRead = true,
		bool canWrite = true,
		bool facts = true,
		bool logs = true) => new()
	{
		Name = name,
		Description = description,
		CanRead = canRead,
		CanWrite = canWrite,
		FactsEnabled = facts,
		LogsEnabled = logs
	};

	private static MemoryBlocksSectionState State(params MemoryBlockItem[] blocks)
		=> new() { Items = [.. blocks] };

	[Fact]
	public async Task NothingChanged_ReturnsNull()
	{
		var provider = CreateProvider(State(Block("Проект dASS")));

		var delta = await provider.CalculateDeltaAsync(State(Block("Проект dASS")), [], CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public async Task ChangedAccess_IsReported()
	{
		var provider = CreateProvider(State(Block("Проект dASS", canRead: true, canWrite: false)));

		var delta = await provider.CalculateDeltaAsync(State(Block("Проект dASS", canRead: true, canWrite: true)), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.AccessChanged);
		Assert.True(updated.Changes.NewCanRead);
		Assert.False(updated.Changes.NewCanWrite);
		Assert.False(updated.Changes.DescriptionChanged);
		Assert.False(updated.Changes.TypesChanged);
	}

	[Fact]
	public async Task ChangedTypes_IsReported()
	{
		var provider = CreateProvider(State(Block("Проект dASS", facts: false, logs: true)));

		var delta = await provider.CalculateDeltaAsync(State(Block("Проект dASS", facts: true, logs: true)), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.TypesChanged);
		Assert.False(updated.Changes.NewFactsEnabled);
		Assert.True(updated.Changes.NewLogsEnabled);
		Assert.False(updated.Changes.AccessChanged);
	}

	[Fact]
	public async Task NewBlock_CarriesFullDefinition()
	{
		var provider = CreateProvider(State(Block("Проект dASS")));

		var delta = await provider.CalculateDeltaAsync(State(), [], CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("Проект dASS", added.Name);
		Assert.NotNull(added.Definition);
		Assert.True(added.Definition!.CanRead);
	}
}
