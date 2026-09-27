namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The announcement of an addon item that was hidden and became visible.
	/// Becoming hidden is never announced: the item stays available, so from the agent's point
	/// of view nothing changes.
	/// </summary>
	public class AddonItemBecameVisible<TItem, TChange>
		where TItem : AddonSectionItem
		where TChange : AddonItemChange<TItem>
	{
		/// <summary>
		/// The item name.
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// The full item, set when the item was never seen before.
		/// Null when only the name (or the field-level changes) should be shown.
		/// </summary>
		public TItem? Definition { get; set; }

		/// <summary>
		/// The field-level changes since the last known definition of the item,
		/// set when the item was seen before, but its definition has changed.
		/// </summary>
		public TChange? Changes { get; set; }
	}
}
