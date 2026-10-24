using System.Collections.Immutable;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	public class ExternalProcessScriptTerminalParameters
	{
		/// <summary>
		/// The kind of addon the script belongs to.
		/// </summary>
		public required ScriptableAddonKind Kind { get; init; }

		/// <summary>
		/// The script body, without the frontmatter.
		/// </summary>
		public required string Body { get; init; }

		/// <summary>
		/// The globals of the preamble, by name.
		/// </summary>
		public ImmutableDictionary<string, INodeValue?> Variables { get; init; } = [];

		/// <summary>
		/// How the globals are handed to the script.
		/// </summary>
		public ScriptInputMode InputMode { get; init; } = ScriptInputMode.Auto;

		/// <summary>
		/// The name shown by the process dispatcher, or <see langword="null"/> for the default.
		/// </summary>
		public string? ProcessName { get; init; }

		/// <summary>
		/// The timeout of the invocation, or <see langword="null"/> for none. A context passes <see langword="null"/>:
		/// its budget is a cancellation token supplied by the caller. A terminal passes <see langword="null"/> too.
		/// </summary>
		public TimeSpan? TimeOut { get; init; }

		/// <summary>
		/// Whether the call waits for the process to exit. A detached terminal returns immediately, reports
		/// <see cref="ExternalProcessStatus.Succeeded"/> with <c>ExitCode == null</c> and keeps running.
		/// </summary>
		public bool Wait { get; init; } = true;
	}
}
