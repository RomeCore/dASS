using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// One scripting language of the out-of-process family.
	/// </summary>
	/// <remarks>
	/// An engine knows its language and nothing else: how to build a preamble in its syntax, how to serialise a value
	/// as a literal, which document encoding its runtime wants and how to launch its interpreter. The variable names
	/// come from the kind adapter and the meaning of the JSON belongs to the kind, so a new language changes neither a
	/// parser nor an adapter.
	/// </remarks>
	public interface IExternalProcessScriptEngine
	{
		/// <summary>
		/// The language this engine handles.
		/// </summary>
		ScriptLanguageType Language { get; }

		/// <summary>
		/// The extensions, frontmatter syntax and capabilities of the language.
		/// </summary>
		IScriptEngineDescriptor Descriptor { get; }

		/// <summary>
		/// Runs the script as a managed process.
		/// </summary>
		Task<ExternalProcessScriptResult> RunAsync(ExternalProcessScriptParameters parameters,
			IServiceProvider services, CancellationToken ct);

		/// <summary>
		/// Runs the script in an interactive terminal and attaches that terminal to <paramref name="host"/>.
		/// </summary>
		Task<ExternalProcessScriptTerminalResult> RunInTerminalAsync(ExternalProcessScriptTerminalParameters parameters,
			IChatAttachmentHost host, IServiceProvider services, CancellationToken ct);
	}
}
