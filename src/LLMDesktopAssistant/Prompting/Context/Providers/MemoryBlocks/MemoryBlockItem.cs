using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// The serializable snapshot of a memory block tracked by the memory blocks section.
	/// </summary>
	public class MemoryBlockItem : AddonSectionItem
	{
		/// <summary>
		/// Whether the block allows reading.
		/// </summary>
		public bool CanRead { get; set; }

		/// <summary>
		/// Whether the block allows writing.
		/// </summary>
		public bool CanWrite { get; set; }

		/// <summary>
		/// Whether the facts type is enabled for the block.
		/// </summary>
		public bool FactsEnabled { get; set; }

		/// <summary>
		/// Whether the logs type is enabled for the block.
		/// </summary>
		public bool LogsEnabled { get; set; }
	}
}
