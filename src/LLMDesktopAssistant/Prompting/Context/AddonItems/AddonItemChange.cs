namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The field-level description of an addon item change. The common fields (description, body)
	/// live here; section-specific fields are handled by the derived change records.
	/// The New* values are expected to be filled only for the fields whose Changed flag is true.
	/// </summary>
	public abstract class AddonItemChange<TItem>
		where TItem : AddonSectionItem
	{
		/// <summary>
		/// Whether the item description has changed.
		/// </summary>
		public bool DescriptionChanged { get; set; }

		/// <summary>
		/// The new item description (when <see cref="DescriptionChanged"/> is true).
		/// </summary>
		public string? NewDescription { get; set; }

		/// <summary>
		/// Whether the item body has changed.
		/// </summary>
		public bool BodyChanged { get; set; }

		/// <summary>
		/// The new item body (when <see cref="BodyChanged"/> is true).
		/// </summary>
		public string? NewBody { get; set; }

		/// <summary>
		/// Returns a new item with the changes applied; the original item is never mutated
		/// (the anchor state and the delta history must stay untouched).
		/// </summary>
		public TItem Apply(TItem known)
		{
			var result = (TItem)known.Clone();
			if (DescriptionChanged)
				result.Description = NewDescription;
			if (BodyChanged)
				result.Body = NewBody;
			ApplyExtra(result);
			return result;
		}

		/// <summary>
		/// Applies the section-specific part of the change to the copied item.
		/// </summary>
		protected virtual void ApplyExtra(TItem item) { }
	}
}
