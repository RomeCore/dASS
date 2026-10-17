namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The streaming analyzer's outcome. Status icon and title go into the call.
	/// </summary>
	public sealed class StreamingToolAnalysisResult
	{
		/// <summary>
		/// Whether the analyzer wants to stop receiving further argument updates.
		/// </summary>
		public bool StopAnalysis { get; init; }

		/// <summary>
		/// Continue receiving updates after this one.
		/// </summary>
		public static StreamingToolAnalysisResult Continue { get; } = new();

		/// <summary>
		/// Stop receiving updates after this one.
		/// </summary>
		public static StreamingToolAnalysisResult Stop { get; } = new() { StopAnalysis = true };
	}
}
