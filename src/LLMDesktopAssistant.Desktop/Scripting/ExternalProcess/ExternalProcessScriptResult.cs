using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The outcome of one managed invocation.
	/// </summary>
	public class ExternalProcessScriptResult
	{
		public required ExternalProcessStatus Status { get; init; }

		/// <summary>
		/// The exit code, or <see langword="null"/> when the process never reported one.
		/// </summary>
		public int? ExitCode { get; init; }

		/// <summary>
		/// Standard output.
		/// </summary>
		public string? Stdout { get; init; }

		/// <summary>
		/// Standard error.
		/// </summary>
		public string? Stderr { get; init; }

		/// <summary>
		/// The merged, arrival-ordered output the launcher produces. It is the raw result when there is no JSON
		/// channel, and stays available for the card and the log when there is one.
		/// </summary>
		public string? Output { get; init; }

		/// <summary>
		/// The parsed structure, or <see langword="null"/> when the addon declared none.
		/// </summary>
		public JsonNode? Json { get; init; }

		/// <summary>
		/// The error message; non-empty exactly when <see cref="Status"/> is not a success.
		/// </summary>
		public string? Error { get; init; }
	}
}
