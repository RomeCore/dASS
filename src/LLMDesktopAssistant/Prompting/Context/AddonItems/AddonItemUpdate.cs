namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The announcement of an addon item definition change (the item stayed available).
	/// </summary>
	public class AddonItemUpdate<TChange>
	{
		/// <summary>
		/// The item name.
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// The field-level changes since the last known definition of the item.
		/// </summary>
		public TChange? Changes { get; set; }
	}
}
