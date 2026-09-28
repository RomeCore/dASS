using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.MemoryBlocks
{
	/// <summary>
	/// The field-level description of a memory block change: the common description field lives
	/// in the base, the access pair and the types pair are block-specific.
	/// </summary>
	public class MemoryBlockChange : AddonItemChange<MemoryBlockItem>
	{
		/// <summary>
		/// Whether the block access (read/write pair) has changed.
		/// </summary>
		public bool AccessChanged { get; set; }

		/// <summary>
		/// The new read permission (when <see cref="AccessChanged"/> is true).
		/// </summary>
		public bool NewCanRead { get; set; }

		/// <summary>
		/// The new write permission (when <see cref="AccessChanged"/> is true).
		/// </summary>
		public bool NewCanWrite { get; set; }

		/// <summary>
		/// Whether the block types (facts/logs pair) have changed.
		/// </summary>
		public bool TypesChanged { get; set; }

		/// <summary>
		/// The new facts flag (when <see cref="TypesChanged"/> is true).
		/// </summary>
		public bool NewFactsEnabled { get; set; }

		/// <summary>
		/// The new logs flag (when <see cref="TypesChanged"/> is true).
		/// </summary>
		public bool NewLogsEnabled { get; set; }

		protected override void ApplyExtra(MemoryBlockItem item)
		{
			if (AccessChanged)
			{
				item.CanRead = NewCanRead;
				item.CanWrite = NewCanWrite;
			}
			if (TypesChanged)
			{
				item.FactsEnabled = NewFactsEnabled;
				item.LogsEnabled = NewLogsEnabled;
			}
		}
	}
}
