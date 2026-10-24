namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Marks a kind's engine interface as a member of the scriptable family.
	/// </summary>
	/// <remarks>
	/// The three scriptable kinds have unrelated engine interfaces, and this marker is what the shared parser constrains
	/// on: it can hold the engine and resolve its descriptor without knowing which kind it serves.
	/// </remarks>
	public interface IScriptableAddonEngine
	{
	}
}
