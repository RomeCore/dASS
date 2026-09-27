namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// A serializable snapshot of an addon (tool, skill, sub-agent, etc.) tracked by an addon section.
	/// Sections extend it with their own fields. The common fields participate in the deltas
	/// for every addon section.
	/// </summary>
	public abstract class AddonSectionItem
	{
		/// <summary>
		/// The addon name (the item identity).
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// The addon description.
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// The addon body (used by the sections that inject it into the prompt, such as skills).
		/// </summary>
		public string? Body { get; set; }

		/// <summary>
		/// Creates a shallow copy of this item: applying changes never mutates the original.
		/// </summary>
		public virtual AddonSectionItem Clone() => (AddonSectionItem)MemberwiseClone();
	}
}
