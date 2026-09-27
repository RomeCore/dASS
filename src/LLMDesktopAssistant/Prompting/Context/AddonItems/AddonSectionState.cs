namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The state of an addon section: the visible items and the names of the hidden items.
	/// The hidden names participate in the deltas (their definitions are never shown).
	/// </summary>
	public abstract class AddonSectionState<TItem> : PromptSectionStateBase
		where TItem : AddonSectionItem
	{
		private List<TItem> _items = [];
		/// <summary>
		/// The visible items of the section.
		/// </summary>
		public List<TItem> Items
		{
			get => _items;
			set => SetProperty(ref _items, value);
		}

		private List<string> _hiddenNames = [];
		/// <summary>
		/// The names of the hidden items available to the agent.
		/// Only their names participate in the deltas; their definitions are never shown.
		/// </summary>
		public List<string> HiddenNames
		{
			get => _hiddenNames;
			set => SetProperty(ref _hiddenNames, value);
		}
	}
}
