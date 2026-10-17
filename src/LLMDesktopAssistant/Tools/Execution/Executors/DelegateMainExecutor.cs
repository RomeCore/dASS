using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Executors
{
	/// <summary>
	/// The main executor built from a plain delegate. It is the single adapter used by both worlds:
	/// chat tools and <c>DelegateAgentTool</c> become an instance of it instead of carrying their own
	/// executor classes.
	/// </summary>
	/// <remarks>
	/// The delegate receives the effective arguments, the invocation context and the cancellation token;
	/// it writes its output into <see cref="IToolExecutionContext.Call"/> and returns whether it
	/// succeeded. There is no completion handle — awaiting this task is the completion.
	/// </remarks>
	public sealed class DelegateMainExecutor : IToolMainExecutor
	{
		private readonly Func<JsonNode?, IToolExecutionContext, CancellationToken, Task<ToolExecutionResult>> _execute;

		/// <summary>
		/// Creates the executor from an asynchronous delegate.
		/// </summary>
		/// <param name="execute">The delegate that runs the tool.</param>
		public DelegateMainExecutor(
			Func<JsonNode?, IToolExecutionContext, CancellationToken, Task<ToolExecutionResult>> execute)
		{
			_execute = execute;
		}

		/// <summary>
		/// Creates the executor from a synchronous delegate.
		/// </summary>
		/// <param name="execute">The delegate that runs the tool.</param>
		/// <returns>A main executor that completes synchronously.</returns>
		public static DelegateMainExecutor FromResult(
			Func<JsonNode?, IToolExecutionContext, CancellationToken, ToolExecutionResult> execute) =>
			new((args, ctx, ct) => Task.FromResult(execute(args, ctx, ct)));

		/// <summary>
		/// Executes the tool with the arguments the pipeline validated.
		/// </summary>
		/// <param name="args">The effective arguments.</param>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="ct">Cancellation of the invocation.</param>
		/// <returns>Whether the tool succeeded.</returns>
		public Task<ToolExecutionResult> ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct) =>
			_execute(args, ctx, ct);
	}
}
