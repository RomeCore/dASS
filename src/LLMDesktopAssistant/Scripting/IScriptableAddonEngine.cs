namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Marks a kind's engine interface as a member of the scriptable family.
	/// </summary>
	public interface IScriptableAddonEngine
	{
		/// <summary>
		/// The languages this engine serves: extensions, frontmatter syntax and capabilities, one descriptor per
		/// language. A multi-language adapter answers with the union; a single-language engine answers with one item.
		/// </summary>
		IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; }
	}
}
