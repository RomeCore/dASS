using LLMDesktopAssistant.LLM.Settings;
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

		/// <summary>
		/// Builds the script and launches it in a terminal, without attaching or waiting.
		/// </summary>
		/// <remarks>
		/// The caller owns the process; the generated script is deleted when the process exits.
		/// </remarks>
		/// <param name="launchCancellationToken">Cancels the launch and, afterwards, kills the process.</param>
		ExternalProcessScriptStartResult StartInTerminal(ExternalProcessScriptTerminalParameters parameters,
			IServiceProvider services, CancellationToken launchCancellationToken = default);

		/// <summary>
		/// Checks that the language's interpreter is present and asks it for its version.
		/// </summary>
		/// <remarks>
		/// The answer is cached per configured environment, so a caller may treat this as cheap; the probe itself never
		/// takes the caller's token, because one chat's Stop must not poison the cache for the others.
		/// </remarks>
		/// <param name="useAddonEnvironment">
		/// Whether the scriptable addons' environment applies instead of the native tools' one. Only python has two.
		/// </param>
		Task<ScriptEngineAvailability> CheckAvailabilityAsync(ChatEnvironmentSettings settings,
			bool useAddonEnvironment = true, CancellationToken cancellationToken = default);
	}
}
