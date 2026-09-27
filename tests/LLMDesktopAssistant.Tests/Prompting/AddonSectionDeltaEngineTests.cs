using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the common addon section delta engine: availability transitions (including hidden
/// items), visibility transitions, field-level changes and delta folding.
/// </summary>
[Collection("Prompting")]
public class AddonSectionDeltaEngineTests
{
	private sealed class FakeItem : AddonSectionItem
	{
		public string? Extra { get; set; }
	}

	private sealed class FakeChange : AddonItemChange<FakeItem>
	{
		public bool ExtraChanged { get; set; }
		public string? NewExtra { get; set; }

		protected override void ApplyExtra(FakeItem item)
		{
			if (ExtraChanged)
				item.Extra = NewExtra;
		}
	}

	private sealed class FakeDelta : AddonSectionDelta<FakeItem, FakeChange>
	{
	}

	private static FakeChange? Diff(FakeItem known, FakeItem current)
	{
		bool descriptionChanged = known.Description != current.Description;
		bool bodyChanged = known.Body != current.Body;
		bool extraChanged = known.Extra != current.Extra;
		if (!descriptionChanged && !bodyChanged && !extraChanged)
			return null;

		return new FakeChange
		{
			DescriptionChanged = descriptionChanged,
			NewDescription = descriptionChanged ? current.Description : null,
			BodyChanged = bodyChanged,
			NewBody = bodyChanged ? current.Body : null,
			ExtraChanged = extraChanged,
			NewExtra = extraChanged ? current.Extra : null
		};
	}

	private static FakeItem Item(string name,
		string? description = "description",
		string? extra = "extra",
		string? body = null) => new()
	{
		Name = name,
		Description = description,
		Extra = extra,
		Body = body
	};

	private static AddonSectionDeltaResult<FakeItem, FakeChange>? Compute(
		FakeItem[]? anchorItems,
		string[]? anchorHidden,
		AddonSectionDelta<FakeItem, FakeChange>[] existingDeltas,
		FakeItem[] currentItems,
		string[]? currentHidden)
		=> AddonSectionDeltaEngine.Compute(
			anchorItems,
			anchorHidden,
			existingDeltas,
			currentItems,
			currentHidden ?? [],
			Diff);

	[Fact]
	public void NothingChanged_ReturnsNull()
	{
		var result = Compute([Item("fs-edit")], [], [], [Item("fs-edit")], []);

		Assert.Null(result);
	}

	[Fact]
	public void NewItem_CarriesFullDefinition()
	{
		var result = Compute(null, null, [], [Item("fs-edit", "edits files")], []);

		Assert.NotNull(result);
		var added = Assert.Single(result!.AddedItems);
		Assert.Equal("fs-edit", added.Name);
		Assert.False(added.Hidden);
		Assert.NotNull(added.Definition);
		Assert.Equal("edits files", added.Definition!.Description);
		Assert.Null(added.Changes);
		Assert.Empty(result.RemovedItems);
		Assert.Empty(result.BecameVisibleItems);
		Assert.Empty(result.UpdatedItems);
	}

	[Fact]
	public void RemovedItem_IsAnnouncedByName()
	{
		var result = Compute([Item("fs-edit")], [], [], [], []);

		Assert.NotNull(result);
		var removed = Assert.Single(result!.RemovedItems);
		Assert.Equal("fs-edit", removed.Name);
		Assert.Empty(result.AddedItems);
	}

	[Fact]
	public void ReEnabledUnchanged_IsAnnouncedByNameOnly()
	{
		// The item was seen at the anchor, removed by an earlier delta and is now back unchanged.
		var existing = new AddonSectionDelta<FakeItem, FakeChange>[]
		{
			new FakeDelta { RemovedItems = [new AddonItemRemoval { Name = "fs-edit" }] }
		};

		var result = Compute([Item("fs-edit")], [], existing, [Item("fs-edit")], []);

		Assert.NotNull(result);
		var added = Assert.Single(result!.AddedItems);
		Assert.Equal("fs-edit", added.Name);
		Assert.False(added.Hidden);
		Assert.Null(added.Definition);
		Assert.Null(added.Changes);
		Assert.Empty(result.RemovedItems);
	}

	[Fact]
	public void ReEnabledChanged_CarriesFieldChangesOnly()
	{
		var existing = new AddonSectionDelta<FakeItem, FakeChange>[]
		{
			new FakeDelta { RemovedItems = [new AddonItemRemoval { Name = "fs-edit" }] }
		};

		var result = Compute([Item("fs-edit", "old description")], [], existing, [Item("fs-edit", "new description")], []);

		Assert.NotNull(result);
		var added = Assert.Single(result!.AddedItems);
		Assert.Null(added.Definition);
		Assert.NotNull(added.Changes);
		Assert.True(added.Changes!.DescriptionChanged);
		Assert.Equal("new description", added.Changes.NewDescription);
		Assert.False(added.Changes.BodyChanged);
		Assert.False(added.Changes.ExtraChanged);
	}

	[Fact]
	public void UpdatedItem_ReportsOnlyChangedFields()
	{
		var result = Compute([Item("fs-edit", "same description", "old extra")], [], [],
			[Item("fs-edit", "same description", "new extra")], []);

		Assert.NotNull(result);
		var updated = Assert.Single(result!.UpdatedItems);
		Assert.Equal("fs-edit", updated.Name);
		Assert.NotNull(updated.Changes);
		Assert.False(updated.Changes!.DescriptionChanged);
		Assert.Null(updated.Changes.NewDescription);
		Assert.True(updated.Changes.ExtraChanged);
		Assert.Equal("new extra", updated.Changes.NewExtra);
		Assert.Empty(result.AddedItems);
		Assert.Empty(result.BecameVisibleItems);
	}

	[Fact]
	public void UpdatedItem_BodyOnlyChange()
	{
		var result = Compute([Item("skill", "same", "extra", null)], [], [],
			[Item("skill", "same", "extra", "new body")], []);

		Assert.NotNull(result);
		var updated = Assert.Single(result!.UpdatedItems);
		Assert.True(updated.Changes!.BodyChanged);
		Assert.Equal("new body", updated.Changes.NewBody);
		Assert.False(updated.Changes.DescriptionChanged);
		Assert.False(updated.Changes.ExtraChanged);
	}

	[Fact]
	public void FoldedUpdate_SuppressesRepeatedDeltas()
	{
		// The anchor has v1, an earlier delta updated it to v2 and the current state is still v2.
		var existing = new AddonSectionDelta<FakeItem, FakeChange>[]
		{
			new FakeDelta
			{
				UpdatedItems =
				[
					new AddonItemUpdate<FakeChange>
					{
						Name = "fs-edit",
						Changes = new FakeChange { DescriptionChanged = true, NewDescription = "v2" }
					}
				]
			}
		};

		var result = Compute([Item("fs-edit", "v1")], [], existing, [Item("fs-edit", "v2")], []);

		Assert.Null(result);
	}

	[Fact]
	public void Folding_DoesNotMutateAnchorItems()
	{
		// Applying the folded changes must not corrupt the anchor state objects.
		var anchorItem = Item("fs-edit", "v1");
		var existing = new AddonSectionDelta<FakeItem, FakeChange>[]
		{
			new FakeDelta
			{
				AddedItems =
				[
					new AddonItemAddition<FakeItem, FakeChange>
					{
						Name = "fs-edit",
						Changes = new FakeChange { DescriptionChanged = true, NewDescription = "v2" }
					}
				]
			}
		};

		var result = Compute([anchorItem], [], existing, [Item("fs-edit", "v2")], []);

		Assert.Null(result);
		Assert.Equal("v1", anchorItem.Description);
	}

	[Fact]
	public void HiddenItemAppearing_IsAnnouncedByNameOnly()
	{
		var result = Compute([Item("visible")], [], [], [Item("visible")], ["mystery"]);

		Assert.NotNull(result);
		var added = Assert.Single(result!.AddedItems);
		Assert.Equal("mystery", added.Name);
		Assert.True(added.Hidden);
		Assert.Null(added.Definition);
		Assert.Null(added.Changes);
	}

	[Fact]
	public void VisibleItemBecomingHidden_IsSilent()
	{
		var result = Compute([Item("fs-edit")], [], [], [], ["fs-edit"]);

		Assert.Null(result);
	}

	[Fact]
	public void HiddenItemBecomingVisible_CarriesDefinition()
	{
		var result = Compute([], ["fs-edit"], [], [Item("fs-edit")], []);

		Assert.NotNull(result);
		var becameVisible = Assert.Single(result!.BecameVisibleItems);
		Assert.Equal("fs-edit", becameVisible.Name);
		Assert.NotNull(becameVisible.Definition);
		Assert.Null(becameVisible.Changes);
		Assert.Empty(result.AddedItems);
		Assert.Empty(result.RemovedItems);
	}

	[Fact]
	public void FoldedBecameVisible_SuppressesRepeatedDeltas()
	{
		var definition = Item("fs-edit");
		var existing = new AddonSectionDelta<FakeItem, FakeChange>[]
		{
			new FakeDelta
			{
				BecameVisibleItems =
				[
					new AddonItemBecameVisible<FakeItem, FakeChange> { Name = "fs-edit", Definition = definition }
				]
			}
		};

		var result = Compute([], ["fs-edit"], existing, [Item("fs-edit")], []);

		Assert.Null(result);
	}

	[Fact]
	public void HiddenItemDisappearing_IsRemoved()
	{
		var result = Compute([Item("visible")], ["mystery"], [], [Item("visible")], []);

		Assert.NotNull(result);
		var removed = Assert.Single(result!.RemovedItems);
		Assert.Equal("mystery", removed.Name);
		Assert.Empty(result.AddedItems);
	}
}
