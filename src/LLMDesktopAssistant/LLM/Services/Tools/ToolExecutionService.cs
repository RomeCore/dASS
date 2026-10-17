using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Tools;
using Serilog;
using ToolStatus = LLMDesktopAssistant.LLM.Domain.ToolStatus;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	/// <summary>
	/// The transitional chat execution path: it uses the new domain, the carried executors and the
	/// effective-arguments accessor, but still sequences the steps inline.
	/// </summary>
	/// <remarks>
	/// This service is deliberately short-lived: P3 extracts the stage services and P4 replaces it with
	/// <c>ChatToolOrchestrator</c> plus the dispatcher. Keeping it until then preserves the running app
	/// while the tool sweep is in progress.
	/// </remarks>
	[ChatService(typeof(IToolExecutionService))]
	public class ToolExecutionService(
		Chat chat,
		IChatSettingsService chatSettings,
		IAgentManagementService agentManager,
		IToolApprovalService toolApprovalService,
		IToolMemorizationService toolMemorizationService,
		IChatExecutionStatusService executionStatusService) : IToolExecutionService
	{
		private readonly ConcurrentDictionary<ToolSyncKey, SemaphoreSlim> _syncScopes = [];
		private readonly ChatConfirmationSink _confirmationSink = new(executionStatusService);

		/// <summary>
		/// Executes a tool call asynchronously.
		/// </summary>
		/// <param name="partialFunctionToolCall">The model's streaming call, or <see langword="null"/>.</param>
		/// <param name="message">The message that owns the call.</param>
		/// <param name="toolCall">The call to process.</param>
		/// <param name="toolInfo">The tool, or <see langword="null"/> when it does not exist.</param>
		/// <param name="cancellationToken">Cancellation of the run.</param>
		public async Task ExecuteAsync(PartialFunctionToolCall? partialFunctionToolCall,
			AssistantMessage message, ToolCall toolCall, ToolInfo? toolInfo, CancellationToken cancellationToken = default)
		{
			var parameters = new ToolContextCreationParams
			{
				Call = toolCall,
				Descriptor = (IToolDescriptor?)toolInfo ?? MissingToolDescriptor.Instance,
				Source = ToolInvocationSource.ChatRun,
				Host = message,
				RunningInUi = true,
				Confirmation = _confirmationSink
			};

			var senderAgent = message.SenderAgentId != Guid.Empty
				? agentManager.GetAgentDescriptor(message.SenderAgentId)
				: null;

			var consent = new ToolConsentMemorizationContext(toolMemorizationService, chat, toolCall.ToolName,
				senderAgent is null
					? null
					: approved => ToolConsentPersister.MemorizeAlways(senderAgent, chatSettings, toolCall.ToolName, approved));

			var ctx = new ChatToolExecutionContext(chat, message, parameters, consent);
			var descriptor = ctx.Descriptor;

			if (partialFunctionToolCall != null)
				await AnalyzeStreamingArgumentsAsync(partialFunctionToolCall, ctx, cancellationToken);

			toolCall.CanBeCompacted = toolInfo?.CanBeCompacted ?? false;

			var keys = CollectSyncKeys(ctx);
			var acquired = new List<SemaphoreSlim>(keys.Count);

			try
			{
				foreach (var key in keys)
				{
					var scope = _syncScopes.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
					await scope.WaitAsync(cancellationToken);
					acquired.Add(scope);
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (await RunPreviewAsync(ctx, cancellationToken))
					return;

				toolCall.ExpectedBehaviour ??= descriptor.DefaultExpectedBehaviour;
				cancellationToken.ThrowIfCancellationRequested();

				var decision = EvaluatePolicy(ctx, senderAgent, out var policyReason);
				ctx.PolicyDecision = decision;
				ctx.PolicyReason = policyReason;
				cancellationToken.ThrowIfCancellationRequested();

				var handled = descriptor.HandledDecisions;
				if (decision == ToolPolicyDecision.Disallow && !handled.HasFlag(ToolPolicyDecision.Disallow))
				{
					toolCall.Status = ToolStatus.Error;
					toolCall.ResultContent = policyReason?.Message ?? "The tool execution was denied by the policy.";
					return;
				}

				string? additionalNotes = null;
				if (decision == ToolPolicyDecision.Ask && !handled.HasFlag(ToolPolicyDecision.Ask))
				{
					var approval = await _confirmationSink.RequestAsync(ctx, cancellationToken);
					MemorizeConsent(senderAgent, toolCall.ToolName, approval);

					if (!approval.Approved)
					{
						toolCall.Status = ToolStatus.Cancelled;
						toolCall.ResultContent = string.IsNullOrWhiteSpace(approval.Notes)
							? "User has cancelled the tool execution without a reason. " +
							"Maybe it can be dangerous or unwanted to proceed."
							: $"User has cancelled the tool execution with a reason: {approval.Notes}.";
						return;
					}

					additionalNotes = approval.Notes;
				}

				toolCall.Status = ToolStatus.Executing;
				cancellationToken.ThrowIfCancellationRequested();

				var result = await descriptor.Main.ExecuteAsync(ctx.Arguments.Effective, ctx, cancellationToken);
				Settle(toolCall, result.Success, additionalNotes, cancellationToken);
			}
			catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is OperationCanceledException))
			{
				MarkCancelled(toolCall);
			}
			catch (OperationCanceledException)
			{
				MarkCancelled(toolCall);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Unhandled error during execution of tool '{ToolName}': {Error}", toolCall.ToolName, ex);
				toolCall.Status = ToolStatus.Error;
				var error = DebugHelper.IsDebug ? ex.ToString() : ex.Message;
				toolCall.ResultContent = string.IsNullOrEmpty(toolCall.ResultContent)
					? "Tool execution failed with error: " + error
					: toolCall.ResultContent + "\nTool execution was interrupted with error: " + error;
			}
			finally
			{
				for (var index = acquired.Count - 1; index >= 0; index--)
					acquired[index].Release();
			}
		}

		private async Task AnalyzeStreamingArgumentsAsync(PartialFunctionToolCall partial, IToolExecutionContext ctx,
			CancellationToken cancellationToken)
		{
			ctx.Call.Status = ToolStatus.Pending;
			var analyzer = ctx.Descriptor.Streaming;

			void OnArgsAdded(object? sender, string delta)
			{
				ctx.Call.Arguments = partial.Args;
				if (analyzer is null)
					return;

				try
				{
					// TolerantJsonParser can parse partial (unfinished) JSON too.
					var args = TolerantJsonParser.Parse(ctx.Call.Arguments);
					var analysis = analyzer.AnalyzeArgs(args ?? new JsonObject(), ctx);
					if (analysis.StopAnalysis)
						analyzer = null;
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "Error analyzing arguments: {Error}", ex);
				}
			}

			partial.ArgsPartAdded += OnArgsAdded;
			try
			{
				await partial;
			}
			finally
			{
				partial.ArgsPartAdded -= OnArgsAdded;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		private async Task<bool> RunPreviewAsync(IToolExecutionContext ctx, CancellationToken cancellationToken)
		{
			var preview = ctx.Descriptor.Preview;
			if (preview is null)
				return false;

			try
			{
				ctx.Call.Status = ToolStatus.PreExecuting;
				var result = await preview.PreviewAsync(ctx.Arguments.Effective, ctx, cancellationToken);

				if (result.ExpectedBehaviour is { } expectedBehaviour)
					ctx.Call.ExpectedBehaviour = expectedBehaviour;

				if (result.InterruptingSuccess is not { } success)
					return false;

				ctx.Call.Status = success ? ToolStatus.Success : ToolStatus.Error;
				if (string.IsNullOrEmpty(ctx.Call.ResultContent))
				{
					ctx.Call.ResultContent = success
						? "Tool successfully returned no result."
						: "Tool failed with no result.";
				}

				return true;
			}
			catch (ArgumentException ex)
			{
				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent = ex.Message;
				return true;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Error during preview execution of tool '{ToolName}': {Error}", ctx.Call.ToolName, ex);
				return false;
			}
		}

		private ToolPolicyDecision EvaluatePolicy(IToolExecutionContext ctx, ChatAgentDescriptor? senderAgent,
			out ToolPolicyReason? reason)
		{
			reason = null;
			var descriptor = ctx.Descriptor;

			var autoApprove = ToolBehaviour.None;
			var disallowed = ToolBehaviour.None;

			if (senderAgent is not null)
			{
				var policy = senderAgent.Tools.GetEffectivePolicy(chatSettings.Settings);
				autoApprove = policy.AutoApproveBehaviours;
				disallowed = policy.DisallowedBehaviours;
			}

			var source = ToolDecisionSource.ApprovalLevel;

			if (descriptor.PolicyMask is { } mask)
			{
				var autoApproveBefore = autoApprove;
				var disallowedBefore = disallowed;

				autoApprove |= mask.AutoApproveBehaviours;
				disallowed |= mask.DisallowedBehaviours;
				autoApprove &= ~mask.DisallowedBehaviours;
				disallowed &= ~mask.AutoApproveBehaviours;

				if (autoApprove != autoApproveBefore || disallowed != disallowedBefore)
					source = ToolDecisionSource.PolicyMask;
			}

			var approvalLevel = descriptor.ApprovalLevel;
			var (decision, decisionMessage) = toolApprovalService.ApproveTool(
				approvalLevel, ctx.Call.ExpectedBehaviour, autoApprove, disallowed);

			if (approvalLevel.IsPolicyBased() && descriptor.Specifier is not null && descriptor.Specifiers.Count > 0)
			{
				var specifier = SpecifierEngine.Evaluate(descriptor.Specifiers, descriptor.Specifier,
					ctx.Arguments.Effective, ctx, descriptor.SpecifierParameters, descriptor.SpecifierAggregationMode);

				var combined = SpecifierEngine.Combine(specifier.Verdict, decision,
					descriptor.SpecifierUnionMode ?? SpecifierBehaviourUnionMode.CombineSoft);

				if (combined != decision || specifier.Verdict == SpecifierVerdict.Deny)
					source = ToolDecisionSource.Specifier;

				decision = combined;

				if (decision == ToolPolicyDecision.Disallow && specifier.Verdict == SpecifierVerdict.Deny)
					decisionMessage = specifier.Message ?? decisionMessage;
			}

			if (approvalLevel.IsPolicyBased() &&
				toolMemorizationService.TryGetMemorizedDecision(chat, ctx.Call.ToolName,
					out var memorized, out var memorizedMessage) &&
				decision != ToolPolicyDecision.Disallow)
			{
				decision = memorized;
				source = ToolDecisionSource.Memorization;

				if (memorized == ToolPolicyDecision.Disallow)
					decisionMessage = memorizedMessage ?? "The tool execution was denied by the user.";
			}

			reason = new ToolPolicyReason(source,
				string.IsNullOrWhiteSpace(decisionMessage) ? null : decisionMessage);
			return decision;
		}

		private void MemorizeConsent(ChatAgentDescriptor? senderAgent, string toolName, ToolApprovalResult approval)
		{
			toolMemorizationService.MemorizeConsent(chat, toolName, new ToolConsentResult
			{
				IsApproved = approval.Approved,
				Memorization = approval.Memorization,
				Notes = approval.Notes
			});

			if (approval.Memorization == ToolApprovalMemorization.Always && senderAgent is not null)
				ToolConsentPersister.MemorizeAlways(senderAgent, chatSettings, toolName, approval.Approved);
		}

		private static IReadOnlyList<ToolSyncKey> CollectSyncKeys(IToolExecutionContext ctx)
		{
			var analyzer = ctx.Descriptor.SyncAnalyzer;
			var keys = new List<ToolSyncKey>(ctx.Descriptor.StaticSyncKeys);
			if (analyzer is not null)
				keys.AddRange(analyzer.AnalyzeSyncKeys(ctx.Arguments.Effective, ctx));

			return [.. keys.Distinct().OrderBy(k => k.Kind).ThenBy(k => k.Value, StringComparer.Ordinal)];
		}

		private static void Settle(ToolCall toolCall, bool success, string? additionalNotes,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(toolCall.ResultContent))
			{
				toolCall.ResultContent = toolCall.Status switch
				{
					ToolStatus.Cancelled => "Tool execution was cancelled.",
					_ => success ? "Tool successfully returned no result." : "Tool failed with no result."
				};
			}

			if (additionalNotes is not null)
				toolCall.ResultContent = $"{toolCall.ResultContent}\n\nAdditional notes from user: {additionalNotes}";

			toolCall.Status = cancellationToken.IsCancellationRequested
				? ToolStatus.Cancelled
				: success ? ToolStatus.Success : ToolStatus.Error;
		}

		private static void MarkCancelled(ToolCall toolCall)
		{
			toolCall.Status = ToolStatus.Cancelled;
			if (string.IsNullOrEmpty(toolCall.ResultContent))
				toolCall.ResultContent = "Tool execution was cancelled.";
			else
				toolCall.ResultContent += "\nTool execution was interrupted.";
		}
	}
}
