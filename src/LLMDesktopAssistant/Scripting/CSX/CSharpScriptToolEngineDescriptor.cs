using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// Descriptor for C# Script (.csx) meta tools with YAML frontmatter in <c>/* ... */</c> comment blocks.
	/// </summary>
	public class CSharpScriptToolEngineDescriptor : IScriptableToolEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public string[] Extensions => [ ".csx" ];

		/// <inheritdoc/>
		public string FrontmatterStart => "/*";

		/// <inheritdoc/>
		public string FrontmatterEnd => "*/";
	}
}
