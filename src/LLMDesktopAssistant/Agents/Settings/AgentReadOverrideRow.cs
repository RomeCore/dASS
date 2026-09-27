using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Agents
{
	public class AgentReadOverrideRow : AgentReadRow
	{
		/// <summary>
		/// Whether this row should override the default visibility row settings.
		/// </summary>
		public bool OverrideVisible
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Defines a bitmask for <see cref="VisibleMessages"/> values that should be overriden.
		/// </summary>
		public MessageVisibilityFacet OverridenVisibleMessages
		{
			get;
			set => SetProperty(ref field, value);
		} = MessageVisibilityFacet.Unknown;

		/// <summary>
		/// Defines a bitmask for <see cref="VisibleParts"/> values that should be overriden.
		/// </summary>
		public MessagePartsFacet OverridenVisibleParts
		{
			get;
			set => SetProperty(ref field, value);
		} = MessagePartsFacet.None;
	}
}
