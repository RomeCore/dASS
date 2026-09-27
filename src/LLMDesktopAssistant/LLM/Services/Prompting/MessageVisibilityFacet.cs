namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Defines the facet of (entire) message visibility used to filter entire messages from the effective list.
	/// </summary>
	/// <remarks>
	/// Semantics of the filtering: <br/>
	/// Every mentioned group (in using enum value) combines with logical AND, while flags inside a group combine with logical OR. <br/>
	/// For example: the filter set only to <see cref="Unknown"/>, so it will include all messages, as no groups are specified. <br/>
	/// Another example: The filter set to <see cref="MessagesWithToolCalls"/>, so it will include only messages that contains tool calls.
	/// </remarks>
	[Flags]
	public enum MessageVisibilityFacet
	{
		Unknown = 0,

		// GROUP 1: TOOL CALLS

		/// <summary>
		/// Filters all messages that have tool calls, but not including those that not have tool calls.
		/// </summary>
		MessagesWithToolCalls = 1 << 0,

		/// <summary>
		/// Filters all messages that do not have tool calls, but not including those that have tool calls.
		/// </summary>
		MessagesWithoutToolCalls = 1 << 1,
	}
}
