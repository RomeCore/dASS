using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Agents
{
	public class AgentReadRow : NotifyPropertyChanged
	{
		public bool Visible
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		public MessageVisibilityFacet VisibleMessages
		{
			get;
			set => SetProperty(ref field, value);
		} = MessageVisibilityFacet.Unknown;

		public MessagePartsFacet VisibleParts
		{
			get;
			set => SetProperty(ref field, value);
		} = MessagePartsFacet.Content | MessagePartsFacet.Attachments | MessagePartsFacet.ToolCallFacts;

		public MessageAuthorIdentity Identity
		{
			get;
			set => SetProperty(ref field, value);
		} = MessageAuthorIdentity.Default;
	}
}
