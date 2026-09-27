namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="Visible"></param>
	/// <param name="IsSelf"></param>
	/// <param name="VisibleParts"></param>
	/// <param name="VisibleIdentity"></param>
	public record MessageVisibilityResult(bool Visible, bool IsSelf,
		MessagePartsFacet VisibleParts, MessageAuthorIdentity VisibleIdentity)
	{
		public bool EffectiveVisible => Visible && VisibleParts != MessagePartsFacet.None;
	}
}
