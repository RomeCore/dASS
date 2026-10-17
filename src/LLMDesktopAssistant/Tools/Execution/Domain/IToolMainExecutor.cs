using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Runs the tool. Its task is the completion of the invocation: there is no separate completion
	/// handle, and everything the tool produces goes into the call.
	/// </summary>
	public interface IToolMainExecutor
	{
		/// <summary>
		/// Executes the tool with the arguments the pipeline validated.
		/// </summary>
		/// <param name="args">The effective arguments.</param>
		/// <param name="ctx">The invocation context; the tool writes its output into <see cref="IToolExecutionContext.Call"/>.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>Whether the tool succeeded. The content and payloads are already on the call.</returns>
		Task<ToolExecutionResult> ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct);
	}
}
