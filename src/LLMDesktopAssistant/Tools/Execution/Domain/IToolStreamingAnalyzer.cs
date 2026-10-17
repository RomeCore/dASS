using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Observes partial arguments while they stream in. It writes status into the call and decides
	/// whether it wants to keep observing.
	/// </summary>
	public interface IToolStreamingAnalyzer
	{
		/// <summary>
		/// Analyses the arguments accumulated so far.
		/// </summary>
		/// <param name="partialArgs">The tolerant-parsed partial arguments.</param>
		/// <param name="ctx">The invocation context; status icon and title are written into the call.</param>
		/// <returns>Whether the analyzer wants to stop receiving updates.</returns>
		StreamingToolAnalysisResult AnalyzeArgs(JsonNode? partialArgs, IToolExecutionContext ctx);
	}
}
