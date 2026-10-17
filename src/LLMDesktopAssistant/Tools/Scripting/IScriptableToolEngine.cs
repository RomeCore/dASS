using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can handle scriptable tool addons written in a specific language
	/// (Lua, Python or C# script). Each engine knows how to serialize, deserialize, and create executors for its language.
	/// <para>
	/// Successor of the late <c>IMetaToolEngine</c> - see the epitaph in <c>ScriptableToolParser.cs</c>.
	/// </para>
	/// </summary>
	public interface IScriptableToolEngine
	{
		/// <summary>
		/// The scripting language this engine handles.
		/// </summary>
		ScriptLanguageType Language { get; }

		/// <summary>
		/// A descriptor for this engine, providing additional information about serialization and extensions.
		/// </summary>
		IScriptableToolEngineDescriptor Descriptor { get; }

		/// <summary>
		/// Creates an executor function for the given tool.
		/// The executor is invoked when the LLM calls the tool.
		/// </summary>
		/// <param name="tool">The tool to create an executor for.</param>
		/// <returns>The main executor of the tool.</returns>
		IToolMainExecutor CreateExecutor(ToolInfo tool);
	}
}
