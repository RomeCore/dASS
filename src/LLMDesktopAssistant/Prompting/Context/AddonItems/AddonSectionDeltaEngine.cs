namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The common net-diff engine for addon section deltas. Folds the existing deltas into the
	/// "seen" state (visible items, hidden names and the last known items), then classifies the
	/// current state into additions, removals, visibility gains and field-level updates:
	/// <list type="bullet">
	/// <item><description>an item that was never seen is announced in full;</description></item>
	/// <item><description>an item that was seen and unchanged is announced by name only;</description></item>
	/// <item><description>an item that was seen and changed is announced with the field-level changes;</description></item>
	/// <item><description>becoming hidden is silent — the item stays available;</description></item>
	/// <item><description>a hidden item becoming visible is announced separately (with its definition when needed).</description></item>
	/// </list>
	/// </summary>
	public static class AddonSectionDeltaEngine
	{
		/// <summary>
		/// Computes the delta between the last known state (anchor + existing deltas)
		/// and the current state, or returns null when nothing has changed.
		/// </summary>
		/// <param name="anchorItems">The visible items captured at the anchor.</param>
		/// <param name="anchorHiddenNames">The hidden item names captured at the anchor.</param>
		/// <param name="existingDeltas">The deltas already issued for this anchor.</param>
		/// <param name="currentItems">The visible items captured now.</param>
		/// <param name="currentHiddenNames">The hidden item names captured now.</param>
		/// <param name="diff">The section-specific field-level comparison (null when items are equal).</param>
		public static AddonSectionDeltaResult<TItem, TChange>? Compute<TItem, TChange>(
			IReadOnlyList<TItem>? anchorItems,
			IReadOnlyList<string>? anchorHiddenNames,
			IEnumerable<AddonSectionDelta<TItem, TChange>> existingDeltas,
			IReadOnlyList<TItem> currentItems,
			IReadOnlyList<string> currentHiddenNames,
			Func<TItem, TItem, TChange?> diff)
			where TItem : AddonSectionItem
			where TChange : AddonItemChange<TItem>
		{
			// What the agent has already seen: the visible set, the names of the hidden items
			// and the last known items. Known items survive leaving the visible set,
			// so a re-appearing item is compared against its previously shown definition.
			var seenVisible = new HashSet<string>(StringComparer.Ordinal);
			var seenHidden = new HashSet<string>(StringComparer.Ordinal);
			var believedItems = new Dictionary<string, TItem>(StringComparer.Ordinal);

			if (anchorItems is not null)
			{
				foreach (var item in anchorItems)
				{
					seenVisible.Add(item.Name);
					believedItems[item.Name] = item;
				}
			}
			if (anchorHiddenNames is not null)
			{
				foreach (var name in anchorHiddenNames)
					seenHidden.Add(name);
			}

			foreach (var delta in existingDeltas)
			{
				foreach (var added in delta.AddedItems)
				{
					if (added.Hidden)
					{
						seenHidden.Add(added.Name);
						seenVisible.Remove(added.Name);
					}
					else
					{
						if (added.Definition is { } definition)
							believedItems[added.Name] = definition;
						else if (added.Changes is { } changes && believedItems.TryGetValue(added.Name, out var known))
							believedItems[added.Name] = changes.Apply(known);

						seenVisible.Add(added.Name);
						seenHidden.Remove(added.Name);
					}
				}

				foreach (var removed in delta.RemovedItems)
				{
					seenVisible.Remove(removed.Name);
					seenHidden.Remove(removed.Name);
				}

				foreach (var becameVisible in delta.BecameVisibleItems)
				{
					if (becameVisible.Definition is { } definition)
						believedItems[becameVisible.Name] = definition;
					else if (becameVisible.Changes is { } changes && believedItems.TryGetValue(becameVisible.Name, out var known))
						believedItems[becameVisible.Name] = changes.Apply(known);

					seenHidden.Remove(becameVisible.Name);
					seenVisible.Add(becameVisible.Name);
				}

				foreach (var update in delta.UpdatedItems)
				{
					if (update.Changes is { } changes && believedItems.TryGetValue(update.Name, out var known))
						believedItems[update.Name] = changes.Apply(known);
				}
			}

			var currentNames = new HashSet<string>(StringComparer.Ordinal);
			var currentHidden = new HashSet<string>(currentHiddenNames, StringComparer.Ordinal);

			var addedItems = new List<AddonItemAddition<TItem, TChange>>();
			var removedItems = new List<AddonItemRemoval>();
			var becameVisibleItems = new List<AddonItemBecameVisible<TItem, TChange>>();
			var updatedItems = new List<AddonItemUpdate<TChange>>();

			foreach (var item in currentItems)
			{
				currentNames.Add(item.Name);

				if (seenVisible.Contains(item.Name))
				{
					if (!believedItems.TryGetValue(item.Name, out var known))
						continue;

					var change = diff(known, item);
					if (change is not null)
						updatedItems.Add(new AddonItemUpdate<TChange>
						{
							Name = item.Name,
							Changes = change
						});
				}
				else if (seenHidden.Contains(item.Name))
				{
					// The item was hidden and became visible: its availability did not change,
					// only its visibility, so it is announced separately.
					var (definition, changes) = CreatePayload(item, believedItems, diff);
					becameVisibleItems.Add(new AddonItemBecameVisible<TItem, TChange>
					{
						Name = item.Name,
						Definition = definition,
						Changes = changes
					});
				}
				else
				{
					var (definition, changes) = CreatePayload(item, believedItems, diff);
					addedItems.Add(new AddonItemAddition<TItem, TChange>
					{
						Name = item.Name,
						Definition = definition,
						Changes = changes
					});
				}
			}

			foreach (var name in currentHidden)
			{
				// Becoming hidden is not announced: the item stays available, so from the agent's
				// point of view nothing changed. The seen state keeps tracking it as available.
				if (seenVisible.Contains(name) || seenHidden.Contains(name))
					continue;

				addedItems.Add(new AddonItemAddition<TItem, TChange>
				{
					Name = name,
					Hidden = true
				});
			}

			foreach (var name in seenVisible)
			{
				if (!currentNames.Contains(name) && !currentHidden.Contains(name))
					removedItems.Add(new AddonItemRemoval { Name = name });
			}

			foreach (var name in seenHidden)
			{
				if (!currentHidden.Contains(name) && !currentNames.Contains(name))
					removedItems.Add(new AddonItemRemoval { Name = name });
			}

			if (addedItems.Count == 0 && removedItems.Count == 0
				&& becameVisibleItems.Count == 0 && updatedItems.Count == 0)
				return null;

			return new AddonSectionDeltaResult<TItem, TChange>
			{
				AddedItems = addedItems,
				RemovedItems = [.. removedItems.OrderBy(r => r.Name, StringComparer.Ordinal)],
				BecameVisibleItems = becameVisibleItems,
				UpdatedItems = updatedItems
			};
		}

		/// <summary>
		/// Creates the definition payload of an announcement: the full item for an item that was
		/// never seen, the field-level changes for an item whose definition has changed,
		/// or nothing at all for an item whose definition is already known and unchanged.
		/// </summary>
		private static (TItem? Definition, TChange? Changes) CreatePayload<TItem, TChange>(
			TItem current,
			Dictionary<string, TItem> believedItems,
			Func<TItem, TItem, TChange?> diff)
			where TItem : AddonSectionItem
			where TChange : AddonItemChange<TItem>
		{
			if (!believedItems.TryGetValue(current.Name, out var known))
				return (current, null);

			var change = diff(known, current);
			return change is null ? (null, null) : (null, change);
		}
	}
}
