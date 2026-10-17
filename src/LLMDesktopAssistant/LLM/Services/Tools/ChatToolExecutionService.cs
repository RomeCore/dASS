using System.Diagnostics;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels.Tools;
using Serilog;
using ToolStatus = LLMDesktopAssistant.LLM.Domain.ToolStatus;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	/// <summary>
	/// The transitional chat execution path: it builds the invocation context and sequences the
	/// Application-scoped stage services. It owns no stage logic of its own.
	/// </summary>
	/// <remarks>
	/// This service is deliberately short-lived: P4 replaces it with <c>ChatToolOrchestrator</c> plus the
	/// dispatcher. Keeping it until then preserves the running app while the stage services, the
	/// attachments model and the completion model land.
	/// </remarks>
	[ChatService(typeof(IChatToolExecutionService))]
	public class ChatToolExecutionService(
		Chat chat,
		IChatSettingsService chatSettings,
		IAgentManagementService agentManager,
		IToolMemorizationService toolMemorizationService,
		IChatExecutionStatusService executionStatusService,
		IToolStreamingService streaming,
		IToolValidationService validation,
		IToolSynchronizationService synchronization,
		IToolPreviewService preview,
		IToolPolicyService policy,
		IToolConfirmationService confirmation,
		IToolExecutionService execution,
		IToolSettlementService settlement) : IChatToolExecutionService
	{
		private readonly ChatConfirmationSink _confirmationSink = new(executionStatusService);

		private readonly IToolExecutionContextFactory _fallbackFactory =
			new ChatToolContextFactory(chat, toolMemorizationService, agentManager, chatSettings);

		/// <inheritdoc/>
		public async Task ExecuteAsync(PartialFunctionToolCall? partialFunctionToolCall,
			AssistantMessage message, ToolCall toolCall, ToolInfo? toolInfo, CancellationToken cancellationToken = default)
		{
			var senderAgent = message.SenderAgentId != Guid.Empty
				? agentManager.GetAgentDescriptor(message.SenderAgentId)
				: null;

			var effectiveBehaviours = new ToolPolicyMask
			{
				AutoApproveBehaviours = ToolBehaviour.None,
				DisallowedBehaviours = ToolBehaviour.None
			};

			if (senderAgent is not null)
			{
				var senderPolicy = senderAgent.Tools.GetEffectivePolicy(chatSettings.Settings);
				effectiveBehaviours = new ToolPolicyMask
				{
					AutoApproveBehaviours = senderPolicy.AutoApproveBehaviours,
					DisallowedBehaviours = senderPolicy.DisallowedBehaviours
				};
			}

			var parameters = new ToolContextCreationParams
			{
				Call = toolCall,
				Descriptor = (IToolDescriptor?)toolInfo ?? MissingToolDescriptor.Instance,
				Source = ToolInvocationSource.ChatRun,
				Host = message,
				RunningInUi = true,
				Confirmation = _confirmationSink,
				EffectiveBehaviours = effectiveBehaviours,
				ArgumentStream = partialFunctionToolCall is null
					? null
					: new PartialFunctionToolCallArgumentStream(partialFunctionToolCall)
			};

			var contextFactory = toolInfo?.ContextFactory ?? _fallbackFactory;
			var ctx = contextFactory.CreateContext(parameters);

			toolCall.CanBeCompacted = toolInfo?.CanBeCompacted ?? false;

			try
			{
				await streaming.RunAsync(ctx, StreamingMode.Cyclic, cancellationToken);

				if (!validation.Validate(ctx))
				{
					toolCall.Status = ToolStatus.Error;
					toolCall.ResultContent = "The tool arguments are invalid.";
					return;
				}

				await synchronization.RunAsync(ctx, async () =>
				{
					if (!await preview.RunAsync(ctx, cancellationToken))
						return;

					await policy.EvaluateAsync(ctx, cancellationToken);

					if (!await confirmation.ConfirmAsync(ctx, cancellationToken))
						return;

					await execution.RunAsync(ctx, cancellationToken);
				}, cancellationToken);
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
				settlement.Finalize(ctx);
			}
		}

		private static void MarkCancelled(ToolCall toolCall)
		{
			toolCall.Status = ToolStatus.Cancelled;
			toolCall.ResultContent = string.IsNullOrEmpty(toolCall.ResultContent)
				? "Tool execution was cancelled."
				: toolCall.ResultContent + "\nTool execution was interrupted.";
		}
	}
}
