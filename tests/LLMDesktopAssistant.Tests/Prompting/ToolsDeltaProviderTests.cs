using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.AddonItems;
using LLMDesktopAssistant.Prompting.Context.Providers.Tools;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the tools section delta (built on the common addon delta engine): availability
/// transitions (including hidden tools), visibility transitions and field-level definition changes.
/// </summary>
[Collection("Prompting")]
public class ToolsDeltaProviderTests
{
	private sealed class FakeToolsStateProvider : IPromptSectionStateProvider<ToolsSectionState>
	{
		public ToolsSectionState? State { get; init; }

		public ToolsSectionState? CaptureState(ChatAgentDescriptor agent) => State;
	}

	private static ToolsDeltaProvider CreateProvider(ToolsSectionState? state)
		=> new(new FakeToolsStateProvider { State = state });

	private static EffectiveChatContext CreateContext() => new()
	{
		Agent = new ChatAgentDescriptor(),
		Messages = [],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1
	};

	private static ToolItem Tool(string name,
		string description = "description",
		string schema = "{\"type\":\"object\"}") => new()
	{
		Name = name,
		Description = description,
		ArgumentSchema = schema
	};

	private static ToolsSectionState State(params ToolItem[] tools)
		=> new() { Items = [.. tools] };

	private static ToolsSectionState StateWithHidden(IEnumerable<string> hidden, params ToolItem[] tools)
		=> new() { Items = [.. tools], HiddenNames = [.. hidden] };

	[Fact]
	public void NothingChanged_ReturnsNull()
	{
		var tool = Tool("fs-edit");
		var provider = CreateProvider(State(tool));

		var delta = provider.CalculateDelta(State(tool), [], CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void NewTool_CarriesFullDefinition()
	{
		var provider = CreateProvider(State(Tool("fs-edit", "edits files", "{\"type\":\"object\"}")));

		var delta = provider.CalculateDelta(null, [], CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("fs-edit", added.Name);
		Assert.False(added.Hidden);
		Assert.NotNull(added.Definition);
		Assert.Equal("edits files", added.Definition!.Description);
		Assert.Equal("{\"type\":\"object\"}", added.Definition.ArgumentSchema);
		Assert.Null(added.Changes);
		Assert.Empty(delta.RemovedItems);
		Assert.Empty(delta.BecameVisibleItems);
		Assert.Empty(delta.UpdatedItems);
	}

	[Fact]
	public void DisabledTool_IsRemoved()
	{
		var provider = CreateProvider(State());

		var delta = provider.CalculateDelta(State(Tool("fs-edit")), [], CreateContext());

		Assert.NotNull(delta);
		var removed = Assert.Single(delta!.RemovedItems);
		Assert.Equal("fs-edit", removed.Name);
		Assert.Empty(delta.AddedItems);
		Assert.Empty(delta.BecameVisibleItems);
		Assert.Empty(delta.UpdatedItems);
	}

	[Fact]
	public void ReEnabledUnchangedTool_IsAnnouncedByNameOnly()
	{
		// The tool was seen at the anchor, disabled by an earlier delta and is now re-enabled unchanged.
		var provider = CreateProvider(State(Tool("fs-edit")));
		var existing = new[]
		{
			new ToolsSectionDelta { RemovedItems = [new AddonItemRemoval { Name = "fs-edit" }] }
		};

		var delta = provider.CalculateDelta(State(Tool("fs-edit")), existing, CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("fs-edit", added.Name);
		Assert.False(added.Hidden);
		Assert.Null(added.Definition);
		Assert.Null(added.Changes);
		Assert.Empty(delta.RemovedItems);
	}

	[Fact]
	public void ReEnabledChangedTool_CarriesFieldChangesOnly()
	{
		// The tool was disabled and changed while it was away: only the changes are reported.
		var provider = CreateProvider(State(Tool("fs-edit", "new description", "schema")));
		var existing = new[]
		{
			new ToolsSectionDelta { RemovedItems = [new AddonItemRemoval { Name = "fs-edit" }] }
		};

		var delta = provider.CalculateDelta(State(Tool("fs-edit", "old description", "schema")), existing, CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("fs-edit", added.Name);
		Assert.Null(added.Definition);
		Assert.NotNull(added.Changes);
		Assert.True(added.Changes!.DescriptionChanged);
		Assert.Equal("new description", added.Changes.NewDescription);
		Assert.False(added.Changes.ArgumentSchemaChanged);
		Assert.Null(added.Changes.NewArgumentSchema);
	}

	[Fact]
	public void UpdatedTool_ReportsOnlyChangedFields()
	{
		var provider = CreateProvider(State(Tool("fs-edit", "same description", "{\"new\":true}")));

		var delta = provider.CalculateDelta(State(Tool("fs-edit", "same description", "{\"old\":true}")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.Equal("fs-edit", updated.Name);
		Assert.NotNull(updated.Changes);
		Assert.False(updated.Changes!.DescriptionChanged);
		Assert.Null(updated.Changes.NewDescription);
		Assert.True(updated.Changes.ArgumentSchemaChanged);
		Assert.Equal("{\"new\":true}", updated.Changes.NewArgumentSchema);
		Assert.Empty(delta.AddedItems);
		Assert.Empty(delta.RemovedItems);
		Assert.Empty(delta.BecameVisibleItems);
	}

	[Fact]
	public void UpdatedTool_DescriptionOnlyChange()
	{
		var provider = CreateProvider(State(Tool("fs-edit", "new description", "schema")));

		var delta = provider.CalculateDelta(State(Tool("fs-edit", "old description", "schema")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.DescriptionChanged);
		Assert.Equal("new description", updated.Changes.NewDescription);
		Assert.False(updated.Changes.ArgumentSchemaChanged);
		Assert.Null(updated.Changes.NewArgumentSchema);
	}

	[Fact]
	public void FoldedUpdate_SuppressesRepeatedDeltas()
	{
		// The anchor has v1, an earlier delta updated it to v2 and the current state is still v2.
		var provider = CreateProvider(State(Tool("fs-edit", "v2", "s2")));
		var existing = new[]
		{
			new ToolsSectionDelta
			{
				UpdatedItems =
				[
					new AddonItemUpdate<ToolChange>
					{
						Name = "fs-edit",
						Changes = new ToolChange
						{
							DescriptionChanged = true,
							NewDescription = "v2",
							ArgumentSchemaChanged = true,
							NewArgumentSchema = "s2"
						}
					}
				]
			}
		};

		var delta = provider.CalculateDelta(State(Tool("fs-edit", "v1", "s1")), existing, CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void HiddenToolAppearing_IsAnnouncedByNameOnly()
	{
		var provider = CreateProvider(StateWithHidden(["mystery"], Tool("visible")));

		var delta = provider.CalculateDelta(State(Tool("visible")), [], CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("mystery", added.Name);
		Assert.True(added.Hidden);
		Assert.Null(added.Definition);
		Assert.Null(added.Changes);
	}

	[Fact]
	public void VisibleToolBecomingHidden_IsSilent()
	{
		// The tool stays available when it becomes hidden, so nothing is announced.
		var provider = CreateProvider(StateWithHidden(["fs-edit"]));

		var delta = provider.CalculateDelta(State(Tool("fs-edit")), [], CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void HiddenToolBecomingVisible_IsAnnouncedWithDefinition()
	{
		var provider = CreateProvider(State(Tool("fs-edit", "description", "schema")));

		var delta = provider.CalculateDelta(StateWithHidden(["fs-edit"]), [], CreateContext());

		Assert.NotNull(delta);
		var becameVisible = Assert.Single(delta!.BecameVisibleItems);
		Assert.Equal("fs-edit", becameVisible.Name);
		// The definition was never delivered while the tool was hidden, so it is carried in full.
		Assert.NotNull(becameVisible.Definition);
		Assert.Null(becameVisible.Changes);
		Assert.Empty(delta.AddedItems);
		Assert.Empty(delta.RemovedItems);
	}

	[Fact]
	public void FoldedBecameVisible_SuppressesRepeatedDeltas()
	{
		var definition = Tool("fs-edit", "description", "schema");
		var provider = CreateProvider(State(definition));
		var existing = new[]
		{
			new ToolsSectionDelta
			{
				BecameVisibleItems =
				[
					new AddonItemBecameVisible<ToolItem, ToolChange> { Name = "fs-edit", Definition = definition }
				]
			}
		};

		var delta = provider.CalculateDelta(StateWithHidden(["fs-edit"]), existing, CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void SilentlyHiddenTool_WithChangedDefinition_ReportsOnlyUpdate()
	{
		// The tool was silently hidden, its definition changed and it is visible again:
		// from the agent's point of view only the definition has changed.
		var provider = CreateProvider(State(Tool("fs-edit", "new description", "schema")));

		var delta = provider.CalculateDelta(State(Tool("fs-edit", "old description", "schema")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.DescriptionChanged);
		Assert.Empty(delta.BecameVisibleItems);
		Assert.Empty(delta.AddedItems);
		Assert.Empty(delta.RemovedItems);
	}

	[Fact]
	public void HiddenToolDisappearing_IsRemoved()
	{
		var provider = CreateProvider(State(Tool("visible")));

		var delta = provider.CalculateDelta(StateWithHidden(["mystery"], Tool("visible")), [], CreateContext());

		Assert.NotNull(delta);
		var removed = Assert.Single(delta!.RemovedItems);
		Assert.Equal("mystery", removed.Name);
		Assert.Empty(delta.AddedItems);
	}
}
