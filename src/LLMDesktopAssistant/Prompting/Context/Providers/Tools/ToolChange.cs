using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// The field-level description of a tool definition change: the common description/body fields
	/// live in the base, only the argument schema is tool-specific.
	/// </summary>
	public class ToolChange : AddonItemChange<ToolItem>
	{
		/// <summary>
		/// Whether the tool argument schema has changed.
		/// </summary>
		public bool ArgumentSchemaChanged { get; set; }

		/// <summary>
		/// The new tool argument schema (when <see cref="ArgumentSchemaChanged"/> is true).
		/// </summary>
		public string? NewArgumentSchema { get; set; }

		protected override void ApplyExtra(ToolItem item)
		{
			if (ArgumentSchemaChanged)
				item.ArgumentSchema = NewArgumentSchema ?? item.ArgumentSchema;
		}
	}
}
