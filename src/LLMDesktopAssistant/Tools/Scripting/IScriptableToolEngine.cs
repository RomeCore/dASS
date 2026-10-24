using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can handle scriptable tool addons written in a specific language
	/// (Lua, Python, Bash, ...). Each engine knows how to build an executor for its language.
	/// <para>
	/// Successor of the late <c>IMetaToolEngine</c> - see the epitaph in <c>ScriptableToolParser.cs</c>.
	/// </para>
	/// </summary>
	public interface IScriptableToolEngine
	{
		/// <summary>
		/// The languages this engine serves: extensions, frontmatter syntax and capabilities, one descriptor per
		/// language. A multi-language adapter answers with the union; a single-language engine answers with one item.
		/// </summary>
		IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; }

		/// <summary>
		/// Creates an executor function for the given tool.
		/// The executor is invoked when the LLM calls the tool.
		/// </summary>
		/// <param name="tool">The tool to create an executor for.</param>
		/// <returns>The main executor of the tool.</returns>
		IToolMainExecutor CreateExecutor(ToolInfo tool);
	}
}
