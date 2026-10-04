using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Tools;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	[ChatService(typeof(IToolExecutionService))]
	public class ToolExecutionService(
		Chat chat,
		IChatSettingsService chatSettings,
		IAgentManagementService agentManager,
		IToolApprovalService toolApprovalService,
		IToolMemorizationService toolMemorizationService,
		IChatExecutionStatusService executionStatusService
	) : IToolExecutionService
	{
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _synchronizationGroups = [];

		public async Task ExecuteAsync(PartialFunctionToolCall? partialFunctionToolCall,
			AssistantMessage message, ToolCall toolCall, ToolInfo? toolInfo, CancellationToken cancellationToken = default)
		{
			object? sharedContext = null;

			if (partialFunctionToolCall != null)
			{
				toolCall.Status = ToolStatus.Pending;
				Func<JsonNode, ToolExecutionContext, StreamingToolArgumentsAnalysisResult>?
					streamingArgumentsAnalyser = toolInfo?.StreamingAnalyzer;

				var streamingToolExecutionContext = new ToolExecutionContext
				{
					Call = toolCall,
					Chat = chat,
					Info = toolInfo!,
					Message = message,
					SharedContext = sharedContext,
					RunningInUI = true,
					PolicyDecision = ToolPolicyDecision.None
				};

				void AddedPartialArg(object? sender, string deltaArg)
				{
					toolCall.Arguments = partialFunctionToolCall.Args;

					if (streamingArgumentsAnalyser != null)
					{
						try
						{
							// TolerantJsonParser can parse partial (unfinished) JSON too!
							var args = TolerantJsonParser.Parse(toolCall.Arguments);
							var analysisResult = streamingArgumentsAnalyser.Invoke(args ?? new JsonObject(),
								streamingToolExecutionContext);

							if (analysisResult.StopAnalysis)
								streamingArgumentsAnalyser = null;

							toolCall.StatusIcon = analysisResult.StatusIcon;
							toolCall.StatusTitle = analysisResult.StatusTitle;
						}
						catch (Exception ex)
						{
							Log.Debug(ex, "Error analyzing arguments: {Error}", ex);
						}
					}
				}

				partialFunctionToolCall.ArgsPartAdded += AddedPartialArg;
				try
				{
					await partialFunctionToolCall;
				}
				finally
				{
					partialFunctionToolCall.ArgsPartAdded -= AddedPartialArg;
				}

				sharedContext = streamingToolExecutionContext.SharedContext;
			}

			if (toolInfo == null)
			{
				toolCall.ResultContent = $"Error: Tool '{toolCall.ToolName}' not found.";
				toolCall.Status = ToolStatus.Error;
				return;
			}

			toolCall.CanBeCompacted = toolInfo.CanBeCompacted;

			var syncSemaphore = toolInfo.SynchronizationGroup is string groupName ?
				_synchronizationGroups.GetOrAdd(groupName, _ => new SemaphoreSlim(1)) :
				null;
			bool semaphoreWaited = false;

			try
			{
				if (syncSemaphore != null)
				{
					await syncSemaphore.WaitAsync(cancellationToken);
					semaphoreWaited = true;
				}
				cancellationToken.ThrowIfCancellationRequested();

				JsonNode? parsedArgs = null;
				var toolHandledDecisions = toolInfo.DefaultSelfHandledDecisions;

				if (toolInfo.PreviewExecutor != null)
				{
					try
					{
						var previewToolExecutionContext = new ToolExecutionContext
						{
							Chat = chat,
							Message = message,
							Call = toolCall,
							Info = toolInfo,
							SharedContext = sharedContext,
							RunningInUI = true,
							PolicyDecision = ToolPolicyDecision.None
						};

						parsedArgs = TolerantJsonParser.Parse(toolCall.Arguments) ?? throw new InvalidOperationException("Invalid JSON format for tool arguments.");
						toolCall.Status = ToolStatus.PreExecuting;
						var preExecutionResult = await toolInfo.PreviewExecutor(parsedArgs, previewToolExecutionContext, cancellationToken);
						sharedContext = previewToolExecutionContext.SharedContext;
						toolCall.StatusTitle = preExecutionResult.StatusTitle;
						toolCall.StatusIcon = preExecutionResult.StatusIcon;

						if (preExecutionResult.ExpectedBehaviour != null)
							toolCall.ExpectedBehaviour = preExecutionResult.ExpectedBehaviour.Value;
						if (preExecutionResult.SelfHandledDecisions != null)
							toolHandledDecisions = preExecutionResult.SelfHandledDecisions.Value;

						if (preExecutionResult.InterruptingSuccess != null)
						{
							toolCall.Status = preExecutionResult.InterruptingSuccess.Value ? ToolStatus.Success : ToolStatus.Error;
							if (!string.IsNullOrEmpty(preExecutionResult.InterruptingContent))
							{
								toolCall.ResultContent = preExecutionResult.InterruptingContent;
								toolCall.UseMarkdown = preExecutionResult.UseMarkdown;
							}
							else
							{
								if (preExecutionResult.InterruptingSuccess.Value)
									toolCall.ResultContent = "Tool successfully returned no result.";
								else
									toolCall.ResultContent = "Tool failed with no result.";
							}
							toolCall.AdditionalData.Reset(preExecutionResult.InterruptingAdditionalData);
							return;
						}
					}
					catch (ArgumentException aex)
					{
						toolCall.Status = ToolStatus.Error;
						toolCall.ResultContent = aex.Message;
						return;
					}
					catch (Exception ex)
					{
						Log.Debug(ex, "Error during preview execution of tool '{ToolName}': {Error}", toolCall.ToolName, ex);
					}
				}

				toolCall.ExpectedBehaviour ??= toolInfo.DefaultExpectedBehaviour;

				cancellationToken.ThrowIfCancellationRequested();

				var senderAgent = agentManager.GetAgentDescriptor(message.SenderAgentId);
				var policy = senderAgent.Tools.GetEffectivePolicy(chatSettings.Settings);

				var autoApproveBehaviours = policy.AutoApproveBehaviours;
				var disallowedBehaviours = policy.DisallowedBehaviours;

				if (toolInfo.PolicyMask is { } policyMask)
				{
					autoApproveBehaviours |= policyMask.AutoApproveBehaviours;
					disallowedBehaviours |= policyMask.DisallowedBehaviours;
					autoApproveBehaviours &= ~policyMask.DisallowedBehaviours;
					disallowedBehaviours &= ~policyMask.AutoApproveBehaviours;
				}

				var approvalLevel = toolInfo.ApprovalLevel ?? ToolApprovalLevel.PolicyBased;

				// Specifier layer: evaluated only for policy-based approval levels.
				SpecifierVerdict specifierVerdict = SpecifierVerdict.None;
				string? specifierMessage = null;
				if (approvalLevel.IsPolicyBased() && toolInfo.SpecifierAnalyzer != null && toolInfo.Specifiers.Count > 0)
				{
					parsedArgs ??= TolerantJsonParser.Parse(toolCall.Arguments) ??
						throw new InvalidOperationException("Invalid JSON format for tool arguments.");

					var specifierToolExecutionContext = new ToolExecutionContext
					{
						Chat = chat,
						Message = message,
						Call = toolCall,
						Info = toolInfo,
						SharedContext = sharedContext,
						RunningInUI = true,
						PolicyDecision = ToolPolicyDecision.None
					};
					var specifierResult = SpecifierEngine.Evaluate(toolInfo.Specifiers, toolInfo.SpecifierAnalyzer,
						parsedArgs, specifierToolExecutionContext, toolInfo.SpecifierParameters, toolInfo.SpecifierAggregationMode);
					specifierVerdict = specifierResult.Verdict;
					specifierMessage = specifierResult.Message;
					sharedContext = specifierToolExecutionContext.SharedContext;
				}

				var (decision, decisionMessage) = toolApprovalService.ApproveTool(
					approvalLevel, toolCall.ExpectedBehaviour.Value, autoApproveBehaviours, disallowedBehaviours);

				// Combine the specifier verdict with the policy decision.
				if (approvalLevel.IsPolicyBased() && toolInfo.SpecifierAnalyzer != null && toolInfo.Specifiers.Count > 0)
				{
					decision = SpecifierEngine.Combine(specifierVerdict, decision,
						toolInfo.SpecifierUnionMode ?? SpecifierBehaviourUnionMode.CombineSoft);
					if (decision == ToolPolicyDecision.Disallow && specifierVerdict == SpecifierVerdict.Deny)
						decisionMessage = specifierMessage ?? decisionMessage;
				}

				// Apply the memorized user decision (if any), unless the policy already disallowed the tool.
				if (approvalLevel.IsPolicyBased() &&
					toolMemorizationService.TryGetMemorizedDecision(chat, toolCall.ToolName, out var memorizedDecision, out var memorizedMessage) &&
					decision != ToolPolicyDecision.Disallow)
				{
					decision = memorizedDecision;
					if (memorizedDecision == ToolPolicyDecision.Disallow)
						decisionMessage = memorizedMessage ?? "The tool execution was denied by the user.";
				}

				if (decision == ToolPolicyDecision.Disallow && !toolHandledDecisions.HasFlag(ToolPolicyDecision.Disallow))
				{
					toolCall.Status = ToolStatus.Error;
					toolCall.ResultContent = decisionMessage;
					return;
				}
				
				string? additionalNotes = null;

				if (decision == ToolPolicyDecision.Ask && !toolHandledDecisions.HasFlag(ToolPolicyDecision.Ask))
				{
					using var confirmation = executionStatusService.WithConfirmation();

					var tcs = new TaskCompletionSource<ToolConsentResult>(TaskCreationOptions.RunContinuationsAsynchronously);
					toolCall.UserConfirmationSource = tcs;
					toolCall.Status = ToolStatus.WaitingForApproval;

					var consentResult = await tcs.Task.WaitAsync(cancellationToken);
					toolMemorizationService.MemorizeConsent(chat, toolCall.ToolName, consentResult);
					if (consentResult.Memorization == ToolApprovalMemorization.Always)
						ToolConsentPersister.MemorizeAlways(senderAgent, chatSettings, toolCall.ToolName, consentResult.IsApproved);
					if (consentResult.IsApproved)
					{
						additionalNotes = consentResult.Notes;
					}
					else
					{
						toolCall.Status = ToolStatus.Cancelled;
						if (string.IsNullOrWhiteSpace(consentResult.Notes))
							toolCall.ResultContent = "User has cancelled the tool execution without a reason. " +
								"Maybe it can be dangerous or unwanted to proceed.";
						else
							toolCall.ResultContent = $"User has cancelled the tool execution with a reason: {consentResult.Notes}.";
						return;
					}
				}

				toolCall.Status = ToolStatus.Executing;

				try
				{
					parsedArgs ??= TolerantJsonParser.Parse(toolCall.Arguments) ?? throw new InvalidOperationException("Invalid JSON format for tool arguments.");
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error parsing tool arguments. Arguments: {Args}.", toolCall.Arguments);
					throw;
				}

				cancellationToken.ThrowIfCancellationRequested();

				var toolExecutionContext = new ToolExecutionContext
				{
					Chat = chat,
					Message = message,
					Call = toolCall,
					Info = toolInfo,
					SharedContext = sharedContext,
					RunningInUI = true,
					PolicyDecision = decision,
					ConsentContext = new ToolConsentMemorizationContext(toolMemorizationService, chat, toolCall.ToolName,
						approved => ToolConsentPersister.MemorizeAlways(senderAgent, chatSettings, toolCall.ToolName, approved))
				};
				var reactiveResult = await toolInfo.Executor.Invoke(parsedArgs, toolExecutionContext, cancellationToken);

				void OnReactiveResultChanged(object? sender, PropertyChangedEventArgs e)
				{
					switch (e.PropertyName)
					{
						case nameof(reactiveResult.StatusIcon):
							toolCall.StatusIcon = reactiveResult.StatusIcon;
							break;
						case nameof(reactiveResult.StatusTitle):
							toolCall.StatusTitle = reactiveResult.StatusTitle;
							break;
						case nameof(reactiveResult.StructuredResult):
							toolCall.StructuredResult = reactiveResult.StructuredResult;
							break;
						case nameof(reactiveResult.UseMarkdown):
							toolCall.UseMarkdown = reactiveResult.UseMarkdown;
							break;
					}
				}
				void OnReactiveResultContentChanged(object? sender, object? e)
				{
					toolCall.ResultContent = reactiveResult.ResultContent;
				}
				reactiveResult.PropertyChanged += OnReactiveResultChanged;
				reactiveResult.ResultContentLines.CollectionChanged += OnReactiveResultContentChanged;

				toolCall.ReactiveToolResult = reactiveResult;
				toolCall.StatusIcon = reactiveResult.StatusIcon ?? toolCall.StatusIcon;
				toolCall.StatusTitle = reactiveResult.StatusTitle ?? toolCall.StatusTitle;
				toolCall.StructuredResult = reactiveResult.StructuredResult;
				toolCall.UseMarkdown = reactiveResult.UseMarkdown;
				toolCall.ResultContent = reactiveResult.ResultContent;
				var addDataSyncSub = toolCall.AdditionalData.SyncFrom(reactiveResult.AdditionalData);

				bool success = false;
				try
				{
					success = await reactiveResult.Completion;
				}
				finally
				{
					reactiveResult.PropertyChanged -= OnReactiveResultChanged;
					reactiveResult.ResultContentLines.CollectionChanged -= OnReactiveResultContentChanged;
					addDataSyncSub.Dispose();

					toolCall.ReactiveToolResult = null;

					if (string.IsNullOrEmpty(toolCall.ResultContent))
					{
						switch (toolCall.Status)
						{
							default:
								if (success)
									toolCall.ResultContent = "Tool successfully returned no result.";
								else
									toolCall.ResultContent = "Tool failed with no result.";
								break;
							case ToolStatus.Cancelled:
								toolCall.ResultContent = "Tool execution was cancelled.";
								break;
						}
					}

					if (additionalNotes != null)
						toolCall.ResultContent = $"{reactiveResult.ResultContent}\n\nAdditional notes from user: {additionalNotes}";

					toolCall.Status = cancellationToken.IsCancellationRequested ? ToolStatus.Cancelled :
						(success ? ToolStatus.Success : ToolStatus.Error);
				}

				return;
			}
			catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is OperationCanceledException))
			{
				toolCall.Status = ToolStatus.Cancelled;
				if (string.IsNullOrEmpty(toolCall.ResultContent))
					toolCall.ResultContent = "Tool execution was cancelled.";
				else
					toolCall.ResultContent += "\nTool execution was interrupted.";
			}
			catch (OperationCanceledException)
			{
				toolCall.Status = ToolStatus.Cancelled;
				if (string.IsNullOrEmpty(toolCall.ResultContent))
					toolCall.ResultContent = "Tool execution was cancelled.";
				else
					toolCall.ResultContent += "\nTool execution was interrupted.";
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Unhandled error during execution of tool '{ToolName}': {Error}", toolCall.ToolName, ex);
				toolCall.Status = ToolStatus.Error;
				string errMsg = DebugHelper.IsDebug ? ex.ToString() : ex.Message;
				if (string.IsNullOrEmpty(toolCall.ResultContent))
					toolCall.ResultContent = "Tool execution failed with error: " + errMsg;
				else
					toolCall.ResultContent += "\nTool execution was interrupted with error: " + errMsg;
			}
			finally
			{
				if (semaphoreWaited)
					syncSemaphore!.Release();
			}
		}
	}
}