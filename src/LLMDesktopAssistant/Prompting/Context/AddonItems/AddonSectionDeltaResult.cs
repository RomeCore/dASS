namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The transient result of <see cref="AddonSectionDeltaEngine.Compute"/>:
	/// the computed transitions, ready to be packed into a concrete section delta.
	/// </summary>
	public class AddonSectionDeltaResult<TItem, TChange>
		where TItem : AddonSectionItem
		where TChange : AddonItemChange<TItem>
	{
		/// <summary>
		/// The items that became available.
		/// </summary>
		public List<AddonItemAddition<TItem, TChange>> AddedItems { get; init; } = [];

		/// <summary>
		/// The items that became unavailable.
		/// </summary>
		public List<AddonItemRemoval> RemovedItems { get; init; } = [];

		/// <summary>
		/// The items that were hidden and became visible.
		/// </summary>
		public List<AddonItemBecameVisible<TItem, TChange>> BecameVisibleItems { get; init; } = [];

		/// <summary>
		/// The items whose definitions have changed while staying available.
		/// </summary>
		public List<AddonItemUpdate<TChange>> UpdatedItems { get; init; } = [];
	}
}
