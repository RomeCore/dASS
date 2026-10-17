using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Prepares the invocation before policy and execution: computes the expected behaviour, may
	/// pre-compute an artifact into <see cref="IToolExecutionContext.SharedContext"/>, and may
	/// interrupt the invocation.
	/// </summary>
	public interface IToolPreviewExecutor
	{
		/// <summary>
		/// Runs the preview for the effective arguments.
		/// </summary>
		/// <param name="args">The effective arguments.</param>
		/// <param name="ctx">The invocation context; status and content are written into the call.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>Only the control outcome; everything else is already on the call.</returns>
		Task<PreviewToolExecutionResult> PreviewAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
	}
}
