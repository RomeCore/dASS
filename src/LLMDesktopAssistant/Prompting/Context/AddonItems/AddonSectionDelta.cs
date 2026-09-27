namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The delta of an addon section:
	/// <list type="bullet">
	/// <item><description>availability: <see cref="AddedItems"/> and <see cref="RemovedItems"/>;</description></item>
	/// <item><description>visibility: <see cref="BecameVisibleItems"/> (a hidden item moved into the visible set;
	/// becoming hidden is not among the transitions — the item stays available);</description></item>
	/// </list>
	/// plus <see cref="UpdatedItems"/> for field-level definition changes.
	/// The section never touches the frozen prompt content itself; it only announces the changes.
	/// </summary>
	public abstract class AddonSectionDelta<TItem, TChange> : PromptSectionDeltaBase
		where TItem : AddonSectionItem
		where TChange : AddonItemChange<TItem>
	{
		/// <summary>
		/// The items that became available (they were not usable before).
		/// </summary>
		public List<AddonItemAddition<TItem, TChange>> AddedItems { get; set; } = [];

		/// <summary>
		/// The items that became unavailable (they cannot be used anymore).
		/// </summary>
		public List<AddonItemRemoval> RemovedItems { get; set; } = [];

		/// <summary>
		/// The items that were hidden and became visible.
		/// </summary>
		public List<AddonItemBecameVisible<TItem, TChange>> BecameVisibleItems { get; set; } = [];

		/// <summary>
		/// The items whose definitions have changed while staying available.
		/// </summary>
		public List<AddonItemUpdate<TChange>> UpdatedItems { get; set; } = [];
	}
}
