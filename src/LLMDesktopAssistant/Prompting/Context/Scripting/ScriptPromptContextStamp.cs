namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// The stamp of a scriptable supersede prompt context. The rendered text is placed into
	/// <see cref="PromptSupersedeStampBase.Snapshot"/> in advance, so <c>Render</c> is a no-op and the
	/// type only marks where the stamp came from (BSON stores the concrete type, no attribute needed).
	/// </summary>
	public class ScriptPromptContextStamp : PromptSupersedeStampBase
	{
	}
}
