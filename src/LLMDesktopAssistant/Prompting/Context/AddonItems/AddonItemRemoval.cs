namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The announcement of an addon item that is no longer available (it cannot be used anymore).
	/// </summary>
	public class AddonItemRemoval
	{
		/// <summary>
		/// The item name.
		/// </summary>
		public string Name { get; set; } = string.Empty;
	}
}
