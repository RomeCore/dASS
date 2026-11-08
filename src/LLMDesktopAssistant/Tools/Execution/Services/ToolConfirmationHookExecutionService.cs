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
	/// The confirmation hook point: fires <c>tool.confirm.{source}.{pending}</c> before the stage acts on
	/// the decision.
	/// </summary>
	/// <remarks>
	/// This is the only place a hook may change <see cref="IToolExecutionContext.PolicyDecision"/>, and a
	/// refusal ends the invocation here so it is never re-routed to the tool's own approval surface.
	/// Weakening a denial requires the layer to be named in <see cref="ToolConfirmHookVerdict.Override"/>;
	/// a verdict that pierces nothing is logged and ignored.
	/// </remarks>
	[Service(typeof(IToolConfirmationHookExecutionService))]
	public sealed class ToolConfirmationHookExecutionService : IToolConfirmationHookExecutionService
	{
		/// <summary>
		/// The text the model reads when a hook refuses the call without explaining itself.
		/// </summary>
		private const string defaultDenial = "The tool execution was denied by a hook.";

		/// <summary>
		/// The text the model reads when a hook reports that the call cannot be carried out.
		/// </summary>
		private const string defaultFailure = "The tool execution could not be carried out by a hook.";

		/// <inheritdoc/>
		public async Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct = default)
		{
			if (ctx.Services.GetService<IHookDispatcher>() is not { } hookDispatcher)
				return true;

			var hookType = ToolHookEvents.ComposeConfirm(ctx.Source, ctx.PolicyDecision);
			var hookTarget = ToolHookEvents.ComposeTarget(ctx.Descriptor, ctx.Call);

			var result = await hookDispatcher.ExecuteHooksAsync<ToolConfirmHookVerdict>(
				CreatePayload(ctx), hookType, hookTarget, HookExecutionFlow.Sequential, cancellationToken: ct);

			ApplyArguments(ctx, result);

			return ApplyDecision(ctx, result);
		}

		/// <summary>
		/// Applies the hook's decision, or keeps the pending one when it abstained.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="verdict">The verdict of the last sequential hook.</param>
		/// <returns><see langword="false"/> when the call was refused.</returns>
		private static bool ApplyDecision(IToolExecutionContext ctx, HookExecutionResult<ToolConfirmHookVerdict?> result)
		{
			if (result.BlockingMessage is not null)
				return Refuse(ctx, result.BlockingMessage, ToolStatus.Rejected, defaultDenial);

			if (result.Value is null)
				return true;

			switch (result.Value.Decision)
			{
				case null or ToolHookDecision.None:
					return true;

				case ToolHookDecision.Allow:
				case ToolHookDecision.Ask:
					// Only a denial is a layer worth protecting; turning "ask" into "allow" is the point.
					if (ctx.PolicyDecision == ToolPolicyDecision.Disallow && !MayPierce(ctx, result.Value.Override))
					{
						Log.Warning("A confirmation hook on tool '{ToolName}' tried to override a denial from " +
							"{Source} with {Override}; the decision is kept.",
							ctx.Call.ToolName, ctx.PolicyReason?.Source, result.Value.Override);
						return true;
					}

					ctx.PolicyDecision = result.Value.Decision is ToolHookDecision.Allow
						? ToolPolicyDecision.Approve
						: ToolPolicyDecision.Ask;
					ctx.PolicyReason = new ToolPolicyReason(ToolDecisionSource.Hook,
						string.IsNullOrWhiteSpace(result.Value.Reason) ? null : result.Value.Reason);
					return true;

				case ToolHookDecision.Disallow:
					return Refuse(ctx, result.Value.Reason, ToolStatus.Rejected, defaultDenial);

				case ToolHookDecision.Error:
					return Refuse(ctx, result.Value.Reason, ToolStatus.Error, defaultFailure);

				default:
					return true;
			}
		}

		/// <summary>
		/// Whether the verdict may weaken the denial the pipeline arrived with.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="overrides">The deepest layer the verdict declared.</param>
		private static bool MayPierce(IToolExecutionContext ctx, ConfirmOverride overrides)
		{
			if (ctx.Descriptor.ApprovalLevel is ToolApprovalLevel.AlwaysDisallow)
				return overrides is ConfirmOverride.All;

			if (ctx.PolicyReason?.Source is ToolDecisionSource.Memorization)
				return overrides is ConfirmOverride.Memorization or ConfirmOverride.All;

			return true;
		}

		/// <summary>
		/// Ends the invocation with the refusal and records the hook as the decision's source.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="reason">What the hook said, or <see langword="null"/>.</param>
		/// <param name="status">The terminal status of the refusal.</param>
		/// <param name="fallback">The text used when the hook gave no reason.</param>
		/// <returns><see langword="false"/>, so the orchestrator skips the rest of the pipeline.</returns>
		private static bool Refuse(IToolExecutionContext ctx, string? reason, ToolStatus status, string fallback)
		{
			var text = string.IsNullOrWhiteSpace(reason) ? fallback : reason;

			ctx.Call.Status = status;
			ctx.Call.ResultContent = text;
			ctx.PolicyReason = new ToolPolicyReason(ToolDecisionSource.Hook, text);

			if (status is ToolStatus.Rejected)
				ctx.PolicyDecision = ToolPolicyDecision.Disallow;

			return false;
		}

		/// <summary>
		/// Applies the argument set a hook returned through the same two channels the user's own edit uses.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		/// <param name="verdict">The verdict of the last sequential hook.</param>
		private static void ApplyArguments(IToolExecutionContext ctx, HookExecutionResult<ToolConfirmHookVerdict?> result)
		{
			if (result.Value?.Arguments is null)
				return;

			if (result.Value.Arguments is not JsonObject arguments)
			{
				Log.Warning("A confirmation hook on tool '{ToolName}' returned an argument set that is not an " +
					"object and cannot replace the model's arguments.", ctx.Call.ToolName);
				return;
			}

			if (result.Value.PersistsArguments)
			{
				ctx.ArgumentsOverride = null;
				ctx.Call.Arguments = arguments.ToJsonString(JsonSerializationOpts.UnsafeEscaping);
			}
			else
			{
				ctx.ArgumentsOverride = arguments;
			}
		}

		/// <summary>
		/// Builds the document the hooks receive.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		private static ToolConfirmHookPayload CreatePayload(IToolExecutionContext ctx) => new()
		{
			Tool = ctx.Call.ToolName,
			Source = ToolHookEvents.DescribeSource(ctx.Source),
			Decision = ToolHookEvents.DescribeDecision(ctx.PolicyDecision),
			ReasonSource = ToolHookEvents.DescribeReasonSource(ctx.PolicyReason?.Source),
			Reason = ctx.PolicyReason?.Message,
			Arguments = ctx.Arguments.Effective,
			Behaviour = ToolHookEvents.DescribeBehaviour(
				ctx.Call.ExpectedBehaviour ?? ctx.Descriptor.DefaultExpectedBehaviour),
			ChatId = ctx.Chat?.Id,
			AgentId = (ctx.Host as AssistantMessage)?.SenderAgentId
		};
	}
}
