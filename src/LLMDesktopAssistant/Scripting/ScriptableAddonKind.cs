namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// The scriptable addon kind a script belongs to. Informational: a language engine never routes on it.
	/// </summary>
	public enum ScriptableAddonKind
	{
		None = 0,
		Tool,
		SlashCommand,
		PromptContext,
	}
}
