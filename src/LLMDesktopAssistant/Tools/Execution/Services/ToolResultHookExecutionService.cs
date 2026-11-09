using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Hooks;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The result hook point: fires <c>tool.result.{source}.{status}</c> after the call's status and its
	/// model-facing text are final, and lets a hook replace that text.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The outcome is part of the event name, so a hook targets it with a glob - <c>tool.result.*.error</c>
	/// for every failure, <c>tool.result.{chat-run,agent-task}.rejected</c> for the agent's own refusals -
	/// instead of being called for every outcome and branching in its body.
	/// </para>
	/// <para>
	/// Settlement is the caller because it runs on both paths, after the synchronization lock is released,
	/// and only once the text the model will read actually exists. That ordering matters: a hook that reads
	/// the file the call just wrote must not hold the lock of the call it reacts to.
	/// </para>
	/// <para>
	/// Invocations launched by a script are not hooked, for the same reason as at the start point: a script
	/// hook that calls a tool would otherwise re-enter itself through the nested invocation.
	/// </para>
	/// </remarks>
	[Service(typeof(IToolResultHookExecutionService))]
	public class ToolResultHookExecutionService : IToolResultHookExecutionService
	{
		/// <inheritdoc/>
		public async Task RunAsync(IToolExecutionContext ctx, Exception? exception, CancellationToken ct = default)
		{
			if (ctx.Services.GetService<IHookDispatcher>() is not { } hookDispatcher)
				return;

			var hookType = ToolHookEvents.ComposeResult(ctx.Source, ctx.Call.Status);
			var hookTarget = ToolHookEvents.ComposeTarget(ctx.Descriptor, ctx.Call);

			HookExecutionResult<ToolResultHookVerdict?> result;
			if (ct.IsCancellationRequested)
				result = await hookDispatcher.ExecuteHooksAsync<ToolResultHookVerdict>(
					CreatePayload(ctx, exception), hookType, hookTarget, HookExecutionFlow.FireAndForget,
					isFlowForced: true, cancellationToken: ct);
			else
				result = await hookDispatcher.ExecuteHooksAsync<ToolResultHookVerdict>(
					CreatePayload(ctx, exception), hookType, hookTarget, HookExecutionFlow.Sequential,
					cancellationToken: ct);

			// A blocking message has nothing to stop here: the call is already over. It is worth a line in the
			// log only because an author who reached for it misunderstood the point.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
				Log.Debug("A hook of type {HookType} on tool {ToolName} asked to block the result, but the call is " +
					"already settled; the message is ignored: {Message}",
					hookType, ctx.Call.ToolName, result.BlockingMessage);

			if (result.Value is not { } verdict)
				return;

			if (!string.IsNullOrWhiteSpace(verdict.Result))
				ctx.Call.ResultContent = verdict.Result;

			if (verdict.Markdown is { } markdown)
				ctx.Call.UseMarkdown = markdown;
		}

		/// <summary>
		/// Builds the document the hooks receive.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="exception">The exception the call ended with, or <see langword="null"/>.</param>
		/// <returns>The payload of one <c>tool.result</c> event.</returns>
		private static ToolResultHookPayload CreatePayload(IToolExecutionContext ctx, Exception? exception)
		{
			var call = ctx.Call;

			return new ToolResultHookPayload
			{
				Tool = ctx.Descriptor.Name,
				Source = ToolHookEvents.DescribeSource(ctx.Source),
				Status = ToolHookEvents.DescribeStatus(call.Status),
				Arguments = ctx.Arguments.Effective,
				Behaviour = ToolHookEvents.DescribeBehaviour(
					call.ExpectedBehaviour ?? ctx.Descriptor.DefaultExpectedBehaviour),
				Result = call.ResultContent,
				Markdown = call.UseMarkdown,
				Error = exception?.Message,
				ErrorType = exception?.GetType().Name,
				ChatId = ctx.Chat?.Id,
				AgentId = (ctx.Host as AssistantMessage)?.SenderAgentId
			};
		}
	}
}
