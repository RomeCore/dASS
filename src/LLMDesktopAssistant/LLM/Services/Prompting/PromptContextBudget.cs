using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// The timeout budget of one prompt context and its failure isolation. It lives at the <em>call site</em> —
	/// the composer and the two section processors — because that is where a context is used, and because "a
	/// context never breaks the prompt build" is a property of the pipeline, not of a language.
	/// </summary>
	/// <remarks>
	/// A provider only runs its body: it arms nothing and catches nothing. This guard decides how long the body
	/// may run, and turns a timeout, a script error and a broken output into "nothing to inject" with a single
	/// warning. A raw failure never escapes into the prompt, and the hard ceiling is enforced here, once.
	/// </remarks>
	public static class PromptContextBudget
	{
		/// <summary>
		/// Runs one provider call under the context's budget.
		/// </summary>
		/// <typeparam name="T">The provider's payload: the context text, a stamp or a delta.</typeparam>
		/// <param name="context">The execution context; its addon carries the budget and the name for the log.</param>
		/// <param name="run">The provider call, armed with the guarded token.</param>
		/// <param name="cancellationToken">The caller's token — the prompt build itself.</param>
		/// <returns>The payload, or <see langword="null"/> when the context has nothing to contribute.</returns>
		public static async Task<T?> RunAsync<T>(PromptContextExecutionContext context,
			Func<CancellationToken, Task<T?>> run, CancellationToken cancellationToken) where T : class
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(Math.Clamp(context.PromptContext.TimeoutMs, 1, ScriptPromptContextHelper.MaxTimeoutMs));

			try
			{
				return await run(timeout.Token);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Prompt context '{Context}' timed out after {Timeout} ms.",
					context.PromptContext.Name, context.PromptContext.TimeoutMs);
				return null;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Prompt context '{Context}' failed: {Message}",
					context.PromptContext.Name, ex.Message);
				return null;
			}
		}
	}
}
