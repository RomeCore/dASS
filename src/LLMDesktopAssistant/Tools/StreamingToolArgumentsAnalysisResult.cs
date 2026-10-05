using Material.Icons;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Represents the result of a tool's streaming pre-execution check.
	/// </summary>
	public class StreamingToolArgumentsAnalysisResult
	{
		/// <summary>
		/// The status icon to be displayed. This will be shown next to the main title (that contains tool name).
		/// </summary>
		public VisualIconKind? StatusIcon { get; init; }

		/// <summary>
		/// The title of the status that will be shown next to the main title (that contains tool name).
		/// </summary>
		public string? StatusTitle { get; init; }

		/// <summary>
		/// Whether to stop the analysis or not. If <see langword="true"/>, the streaming analyzer will not be executed anymore for current streaming tool call.
		/// </summary>
		public bool StopAnalysis { get; init; } = false;
	}
}