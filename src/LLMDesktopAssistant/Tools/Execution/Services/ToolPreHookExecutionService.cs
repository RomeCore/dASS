using System.Text.Json.Nodes;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Hooks;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// Fires <c>tool.start.{source}</c> before the arguments are validated and the synchronization keys are
	/// derived: a hook may refuse the call or replace its argument set.
	/// </summary>
	/// <remarks>
	/// This is the only point that may rewrite arguments, because every later stage derives its findings
	/// from that set. Calls launched by a script are not hooked: a script hook that calls a tool would
	/// re-enter itself.
	/// </remarks>
	[Service(typeof(IToolPreHookExecutionService))]
	public class ToolPreHookExecutionService : IToolPreHookExecutionService
	{
		/// <summary>
		/// The text the model reads when a hook refuses a call without explaining itself.
		/// </summary>
		private const string defaultDenial = "The tool execution was blocked by a hook.";

		/// <inheritdoc/>
		public async Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			if (ctx.Services.GetService<IHookDispatcher>() is not { } hookDispatcher)
				return true;

			var hookType = ToolHookEvents.ComposeStart(ctx.Source);
			var hookName = ToolHookEvents.ComposeName(ctx.Descriptor, ctx.Call);

			var result = await hookDispatcher.ExecuteHooksAsync<ToolPreUseHookVerdict>(
				CreatePayload(ctx), hookType, hookName, HookExecutionFlow.Sequential, cancellationToken: ct);

			// A hook that broke the chain itself - a blocking exit code - beats any verdict: there is none.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
				return Deny(ctx, result.BlockingMessage, ToolStatus.Rejected);

			if (result.Value is not { } verdict)
				return true;

			var reason = string.IsNullOrWhiteSpace(verdict.Reason) ? defaultDenial : verdict.Reason;

			switch (verdict.Decision)
			{
				case ToolHookDecision.Disallow:
					return Deny(ctx, reason, ToolStatus.Rejected);

				case ToolHookDecision.Error:
					return Deny(ctx, reason, ToolStatus.Error);
			}

			if (verdict.Arguments is JsonObject arguments)
			{
				if (verdict.PersistsArguments)
				{
					ctx.ArgumentsOverride = null;
					ctx.Call.Arguments = arguments.ToJsonString(JsonSerializationOpts.UnsafeEscaping);
				}
				else
					ctx.ArgumentsOverride = arguments;
			}
			else if (verdict.Arguments is not null)
				Log.Warning("Hook {HookType} on tool {ToolName} returned an argument set that is not an object " +
					"and cannot replace the model's arguments.", hookType, ctx.Call.ToolName);

			return true;
		}

		/// <summary>
		/// Builds the document the hooks receive.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		private static ToolPreUseHookPayload CreatePayload(IToolExecutionContext ctx) => new()
		{
			Tool = ToolHookEvents.ComposeName(ctx.Descriptor, ctx.Call),
			Behaviour = ToolHookEvents.DescribeBehaviour(
				ctx.Call.ExpectedBehaviour ?? ctx.Descriptor.DefaultExpectedBehaviour),
			Source = ToolHookEvents.DescribeSource(ctx.Source),
			Arguments = ctx.Arguments.Effective,
			ChatId = ctx.Chat?.Id,
			AgentId = (ctx.Host as AssistantMessage)?.SenderAgentId
		};

		/// <summary>
		/// Fails the call with a model-facing reason and the status the refusal is recorded as.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="reason">What the model is told instead of the tool output.</param>
		/// <param name="status">The terminal status of the refused call.</param>
		/// <returns><see langword="false"/>, so the orchestrator skips the rest of the pipeline.</returns>
		private static bool Deny(IToolExecutionContext ctx, string reason, ToolStatus status)
		{
			ctx.Call.Status = status;
			ctx.Call.ResultContent = reason;
			return false;
		}
	}
}
