using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Python implementation of <see cref="IScriptablePromptContextEngine"/>. Handles contexts written in Python
	/// with YAML frontmatter in <c>"""</c> docstring blocks: the provider it returns runs the body as a headless
	/// external process, stdout is the context text.
	/// Only available on Desktop.
	/// </summary>
	[Service(typeof(IScriptablePromptContextEngine))]
	public class PythonPromptContextEngine(
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService
	) : IScriptablePromptContextEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [new PythonPromptContextEngineDescriptor()];

		/// <inheritdoc/>
		public IPromptContextProvider CreateProvider(PromptContextInfo context) => context.Kind switch
		{
			PromptContextProviderKind.LiveTail =>
				new PythonLiveTailScriptContextProvider(context, processLauncher, pythonHelperService),
			PromptContextProviderKind.Supersede =>
				new PythonSupersedeScriptContextProvider(context, processLauncher, pythonHelperService),
			_ => throw new NotSupportedException(
				$"Scriptable context '{context.Name}' declares kind '{context.Kind}', but only 'live-tail' and " +
				"'supersede' are scriptable. A scriptable anchored section does not exist yet.")
		};
	}

	/// <summary>
	/// The descriptor of the Python scriptable prompt-context engine.
	/// </summary>
	public class PythonPromptContextEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		/// <inheritdoc/>
		public string[] Extensions => [".py"];

		/// <inheritdoc/>
		public string FrontmatterStart => @"""""""";

		/// <inheritdoc/>
		public string FrontmatterEnd => @"""""""";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => null;

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.StructuredOutput |
			ScriptEngineCapabilities.Terminal;
	}
}
