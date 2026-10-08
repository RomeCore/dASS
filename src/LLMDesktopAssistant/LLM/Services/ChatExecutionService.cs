using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Controls.Toasts;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional.Context;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools.Consents;
using Material.Icons;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Messages;
using RCLargeLanguageModels.Metadata;
using RCLargeLanguageModels.Tasks;
using RCLargeLanguageModels.Tools;
using Serilog;

#pragma warning disable CS9113 // Parameter has not been used.

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The default implementation of the <see cref="IChatExecutionService"/>.
	/// </summary>
	[ChatService(typeof(IChatExecutionService))]
	public class ChatExecutionService(
		Chat chat,
		IChatSettingsService chatSettings,
		IChatExecutionTokenService tokens,
		IAddonManagerInvalidator addonInvalidator,
		IAgentOrderingService agentOrderer,
		IAgentManagementService agentManager,
		IChatStorageService storage,
		IAgentPromptComposer promptComposer,
		IModelManager modelManager,
		IToolExecutionService toolExecutor,
		IToolMemorizationService toolMemorizer,
		IEnumerable<IChatExecutionHook> executionHooks,
		IToolsetCacheService toolsetCache,
		IMCPManagementService mcpManager,
		IUsageStatsCollector usageStatsCollector,
		IToastService toastService,
		IChatExecutionStatusService executionStatusService,
		IChatStatusService statusService,
		IPromptDumpService promptDumpService
	) : IChatExecutionService
	{
		private const bool EnablePrefixCompletions = false;

		private readonly List<IChatExecutionHook> _executionHooks = executionHooks.OrderBy(h => h.Order).ToList();

		public async Task GenerateResponseAsync(CancellationToken cancellationToken = default)
		{
			using var execution = executionStatusService.WithExecution();

			try
			{
				int cycles = 0;
				using var scope = tokens.WithToken(ChatExecutionLevel.AgentSequence, cancellationToken, out var token);

				while (true)
				{
					token.ThrowIfCancellationRequested();

					var lastAssistantMessage = chat.Messages.LastOrDefault()?.Message as Domain.AssistantMessage;
					Guid? nextAgentId = lastAssistantMessage != null && lastAssistantMessage.ToolCalls.Count != 0
						? lastAssistantMessage.SenderAgentId
						: null;
					Guid? agentStageId = lastAssistantMessage != null && lastAssistantMessage.ToolCalls.Count != 0
						? lastAssistantMessage.AgentStageId
						: null;

					if (nextAgentId == null || agentStageId == null)
					{
						statusService.Icon = MaterialIconKind.RobotConfused;
						statusService.Text = LocalizationManager.LocalizeStatic("chat.status.selecting_agent");

						var agentTuple = await agentOrderer.GetNextAgentAsync(token);
						nextAgentId = agentTuple?.Item1;
						agentStageId = agentTuple?.Item2;
					}

					if (nextAgentId == null || agentStageId == null)
					{
						if (cycles == 0)
							toastService.ShowWarning(LocalizationManager.LocalizeStatic("chat.toast.agent_selection_failed.title"),
								LocalizationManager.LocalizeStatic("chat.toast.agent_selection_failed.description"));
						else
							await RunExecutionFinishedHooksAsync(token);
						return;
					}

					token.ThrowIfCancellationRequested();
					await GenerateResponseWithAgentAsync(nextAgentId.Value, agentStageId.Value, token);
					cycles++;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (ToastedException tex)
			{
				Log.Error(tex, "An error occurred while generating the response using default agent: {ErrorMessage}", tex.Message);
				throw;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "An error occurred while generating the response using default agent: {ErrorMessage}", ex.Message);
				toastService.ShowError(LocalizationManager.LocalizeStatic("chat.toast.generation_failed.title"),
					LocalizationManager.LocalizeStaticFormat("chat.toast.generation_failed.description", ex.Message));
				throw;
			}
			finally
			{
				statusService.Icon = MaterialIconKind.ChatProcessing;
				statusService.Text = null;
			}
		}

		public async Task GenerateResponseWithAgentAsync(Guid agentId, Guid agentStageId,
			CancellationToken cancellationToken = default)
		{
			try
			{
				using var scope = tokens.WithToken(ChatExecutionLevel.Agent, cancellationToken, out var token);
				var agent = agentManager.GetAgentDescriptor(agentId);
				toolMemorizer.PushTaskAsyncScope();

				var responsesBuilder = ImmutableList.CreateBuilder<Domain.AssistantMessage>();
				int cycles = 0;

				while (true)
				{
					var response = await GenerateMessageResponseAsync(agentId, agentStageId, agent, cycles, token);
					cycles++;
					responsesBuilder.Add(response);

					// Invoke execution-finished hooks (e.g. auto-naming) fire-and-forget
					if (response.ToolCalls.Count == 0)
					{
						await RunAgentExecutionFinishedHooksAsync(new ChatAgentExecutionHookContext
						{
							Chat = chat,
							Agent = agent,
							Responses = responsesBuilder.ToImmutable()
						}, token);
						break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "An error occurred while generating the response using agent: {ErrorMessage}", ex.Message);
				throw;
			}
			finally
			{
				statusService.Icon = MaterialIconKind.ChatProcessing;
				statusService.Text = null;
			}
		}

		private async Task<Domain.AssistantMessage> GenerateMessageResponseAsync(Guid agentId, Guid agentStageId,
			ChatAgentDescriptor agent, int cycle, CancellationToken cancellationToken = default)
		{
			try
			{
				using var scope = tokens.WithToken(ChatExecutionLevel.Message, cancellationToken, out var token);
				addonInvalidator.ReloadIfInvalid(AddonKind.All);

				var modelName = !string.IsNullOrEmpty(agent.Info.CustomModel)
					? agent.Info.CustomModel
					: chatSettings.Settings.Models.GetEffectiveSelection().ChatModel;
				LLModel llm;
				try
				{
					llm = modelManager.GetModel(modelName);
				}
				catch
				{
					var toastTitle = LocalizationManager.LocalizeStatic("chat.toast.llm_not_configured.title");
					var toastDesc = LocalizationManager.LocalizeStatic("chat.toast.llm_not_configured.description");
					toastService.ShowError(toastTitle, toastDesc);
					throw new ToastedException(toastTitle, toastDesc);
				}

				if (mcpManager.HasMCPConnections())
				{
					statusService.Icon = MaterialIconKind.Connection;
					statusService.Text = LocalizationManager.LocalizeStatic("chat.status.waiting_for_mcp_connections");

					await mcpManager.EnsureCurrentMCPConnectionsAsync(token);
				}

				var completionSource = new CompletionSource();
				var domainResponseMessage = new Domain.AssistantMessage
				{
					CreatedAt = DateTime.Now,
					Status = AssistantMessageStatus.Pending,
					SenderAgentId = agentId,
					AgentStageId = agentStageId,
					IsUserLike = agent.Info.IdentifyAsUser,
					CompletionToken = completionSource.Token
				};

				string prefixReasoningContent = string.Empty;
				string prefixContent = string.Empty;

				if (EnablePrefixCompletions && chat.Messages[^1].Message is Domain.AssistantMessage lastAssistantMessage
					&& lastAssistantMessage.SenderAgentId == agentId && lastAssistantMessage.ToolCalls.Count == 0)
				{
					prefixReasoningContent = lastAssistantMessage.ReasoningContent ?? string.Empty;
					prefixContent = lastAssistantMessage.Content ?? string.Empty;

					storage.EditMessage(chat.Messages[^1].MessageIndex, domainResponseMessage);
				}
				else
				{
					storage.AppendMessage(domainResponseMessage);
				}

				var timeRequested = DateTime.Now;
				DateTime? timeFirstToken = null;

				await RunResponsePrepareHooksAsync(new ChatPrepareExecutionHookContext
				{
					Chat = chat,
					Agent = agent,
					Response = domainResponseMessage,
					Cycle = cycle
				}, token);

				var promptBundle = promptComposer.Build(agent);
				var inputMessages = promptBundle.Messages;
				var toolset = promptBundle.Tools;

				statusService.Icon = MaterialIconKind.ChatProcessing;
				statusService.Text = LocalizationManager.LocalizeStatic("chat.status.waiting_for_first_response");

				var response = await llm.ChatStreamingAsync(inputMessages, tools: toolset, cancellationToken: token);
				var responseMessage = response.Message;

				List<Task> toolExecutionTasks = [];
				var lockObj = new object();

				void ProcessToolCall(IToolCall toolCall)
				{
					if (toolCall is not IFunctionToolCall funtionCall)
						throw new InvalidOperationException($"Unsupported tool call type: {toolCall.GetType()}.");

					if (toolsetCache.ValidAliasedTools.TryGetValue(toolCall.ToolName, out var toolInfo))
					{
						if (toolInfo.Name != toolCall.ToolName)
						{
							Log.Information($"Tool call '{toolCall.ToolName}' is aliased as '{toolInfo.Name}'. Using the alias.");
						}
					}

					var toolCallCompletionSource = new CompletionSource();
					var domainToolCall = new Domain.ToolCall
					{
						Status = ToolStatus.None,
						ToolCallId = toolCall.Id,
						ToolName = toolInfo?.Name ?? toolCall.ToolName,
						Title = toolInfo?.NameKey,
						Arguments = funtionCall.Args,
						CompletionToken = toolCallCompletionSource.Token
					};
					domainResponseMessage.ToolCalls.Add(domainToolCall);

					async Task WrapToolExecutionTask()
					{
						try
						{
							await toolExecutor.ExecuteAsync(funtionCall as PartialFunctionToolCall,
								domainResponseMessage, domainToolCall, toolInfo, token);
						}
						finally
						{
							toolCallCompletionSource.Complete();
						}
					}

					var toolExecTask = WrapToolExecutionTask();
					lock (lockObj)
						toolExecutionTasks.Add(toolExecTask);
				}

				void PartHandler(object? s, AssistantMessageDelta delta)
				{
					if (timeFirstToken == null)
					{
						timeFirstToken ??= DateTime.Now;

						statusService.Icon = MaterialIconKind.ChatProcessing;
						statusService.Text = null;
					}

					domainResponseMessage.Status = AssistantMessageStatus.Streaming;

					if (!string.IsNullOrEmpty(delta.DeltaReasoningContent))
						domainResponseMessage.ReasoningContent = prefixReasoningContent + responseMessage.ReasoningContent;
					if (!string.IsNullOrEmpty(delta.DeltaContent))
						domainResponseMessage.Content = prefixContent + responseMessage.Content;

					foreach (var toolCall in delta.NewToolCalls ?? [])
						ProcessToolCall(toolCall);
				}

				domainResponseMessage.ReasoningContent = prefixReasoningContent + responseMessage.ReasoningContent;
				domainResponseMessage.Content = prefixContent + responseMessage.Content;
				foreach (var toolCall in responseMessage.ToolCalls)
					ProcessToolCall(toolCall);

				responseMessage.PartAdded += PartHandler;
				try
				{
					try
					{
						await response;

						timeFirstToken ??= DateTime.Now;
						var timeReponseFinished = DateTime.Now;

						prefixReasoningContent = string.Empty;
						prefixContent = string.Empty;

						var usageMetadata = response.UsageMetadata;
						if (usageMetadata != null)
						{
							if (usageMetadata is IUsageCacheMetadata usageCacheMetadata)
							{
								domainResponseMessage.AdditionalData.Add(new TokenCostViewModel
								{
									ModelName = modelName,
									InputTokens = usageMetadata.InputTokens,
									InputCacheHitTokens = usageCacheMetadata.InputCacheHitTokens,
									InputCacheMissTokens = usageCacheMetadata.InputCacheMissTokens,
									OutputTokens = usageMetadata.OutputTokens,
									TTFT = (timeFirstToken!.Value - timeRequested).TotalSeconds,
									GenerationTime = (timeReponseFinished - timeFirstToken.Value).TotalSeconds,
								});

								usageStatsCollector.RecordUsage(
									model: modelName,
									inputTokens: usageMetadata.InputTokens,
									outputTokens: usageMetadata.OutputTokens,
									cacheHitTokens: usageCacheMetadata.InputCacheHitTokens,
									cacheMissTokens: usageCacheMetadata.InputCacheMissTokens,
									durationMs: (long)(timeReponseFinished - timeRequested).TotalMilliseconds,
									success: true);
							}
							else
							{
								domainResponseMessage.AdditionalData.Add(new TokenCostViewModel
								{
									ModelName = modelName,
									InputTokens = usageMetadata.InputTokens,
									InputCacheHitTokens = null,
									InputCacheMissTokens = null,
									OutputTokens = usageMetadata.OutputTokens,
									TTFT = (timeFirstToken!.Value - timeRequested).TotalSeconds,
									GenerationTime = (timeReponseFinished - timeFirstToken.Value).TotalSeconds,
								});

								usageStatsCollector.RecordUsage(
									model: modelName,
									inputTokens: usageMetadata.InputTokens,
									outputTokens: usageMetadata.OutputTokens,
									durationMs: (long)(timeReponseFinished - timeRequested).TotalMilliseconds,
									success: true);
							}

							await RunResponseCompletedHooksAsync(new ChatAgentResponseExecutionHookContext
							{
								Chat = chat,
								Agent = agent,
								Response = domainResponseMessage,
								UsageMetadata = usageMetadata,
								HasToolCalls = toolExecutionTasks.Count > 0,
								Cycle = cycle
							}, token);
						}
						else
						{
							domainResponseMessage.AdditionalData.Add(new TokenCostViewModel
							{
								ModelName = modelName,
								InputTokens = null,
								InputCacheHitTokens = null,
								InputCacheMissTokens = null,
								OutputTokens = null,
								TTFT = (timeFirstToken!.Value - timeRequested).TotalSeconds,
								GenerationTime = (timeReponseFinished - timeFirstToken.Value).TotalSeconds,
							});
						}

						domainResponseMessage.Status = token.IsCancellationRequested ?
							AssistantMessageStatus.Cancelled : AssistantMessageStatus.Success;
					}
					catch (OperationCanceledException)
					{
						domainResponseMessage.Status = AssistantMessageStatus.Cancelled;
						RecordFailedUsage(modelName, timeRequested, "Operation cancelled");
						throw;
					}
					catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is OperationCanceledException))
					{
						domainResponseMessage.Status = AssistantMessageStatus.Cancelled;
						RecordFailedUsage(modelName, timeRequested, "Operation cancelled");
						throw;
					}
					catch (Exception ex)
					{
						domainResponseMessage.Error = Locale.GetConstKey(ex.ToString());
						domainResponseMessage.Status = AssistantMessageStatus.Error;
						RecordFailedUsage(modelName, timeRequested, ex.Message);
						throw;
					}
					finally
					{
						responseMessage.PartAdded -= PartHandler;
					}
				}
				finally
				{
					try
					{
						await Task.WhenAll(toolExecutionTasks);
					}
					finally
					{
						completionSource.Complete();
						token.ThrowIfCancellationRequested();
					}
				}

				return domainResponseMessage;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "An error occurred while generating the response message using agent: {ErrorMessage}", ex.Message);
				throw;
			}
		}

		/// <summary>
		/// Invokes <see cref="IChatExecutionHook.OnResponsePrepareAsync"/> on all registered
		/// hooks in ascending <see cref="IChatExecutionHook.Order"/>, awaiting each one.
		/// Failures are logged and do not propagate to the execution pipeline.
		/// </summary>
		/// <param name="context">The context of the preview response cycle.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		private async Task RunResponsePrepareHooksAsync(ChatPrepareExecutionHookContext context, CancellationToken cancellationToken)
		{
			foreach (var hook in _executionHooks)
			{
				try
				{
					await hook.OnResponsePrepareAsync(context, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Hook {Hook} failed in OnResponsePrepare: {Error}", hook.GetType().Name, ex.Message);
				}
			}
		}

		/// <summary>
		/// Invokes <see cref="IChatExecutionHook.OnAgentResponseCompletedAsync"/> on all registered
		/// hooks in ascending <see cref="IChatExecutionHook.Order"/>, awaiting each one.
		/// Failures are logged and do not propagate to the execution pipeline.
		/// </summary>
		/// <param name="context">The context of the completed response cycle.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		private async Task RunResponseCompletedHooksAsync(ChatAgentResponseExecutionHookContext context, CancellationToken cancellationToken)
		{
			foreach (var hook in _executionHooks)
			{
				try
				{
					await hook.OnAgentResponseCompletedAsync(context, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Hook {Hook} failed in OnResponseCompleted: {Error}", hook.GetType().Name, ex.Message);
				}
			}
		}

		/// <summary>
		/// Invokes <see cref="IChatExecutionHook.OnAgentExecutionFinishedAsync"/> on all registered
		/// hooks in ascending <see cref="IChatExecutionHook.Order"/> without awaiting them
		/// (fire-and-forget). Failures are logged and do not propagate to the execution pipeline.
		/// </summary>
		/// <param name="context">The context of the finished execution.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		private Task RunAgentExecutionFinishedHooksAsync(ChatAgentExecutionHookContext context, CancellationToken cancellationToken)
		{
			foreach (var hook in _executionHooks)
			{
				try
				{
					_ = hook.OnAgentExecutionFinishedAsync(context, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Hook {Hook} failed in OnExecutionFinished: {Error}", hook.GetType().Name, ex.Message);
				}
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Invokes <see cref="IChatExecutionHook.OnExecutionFinishedAsync"/> on all registered
		/// hooks in ascending <see cref="IChatExecutionHook.Order"/> without awaiting them
		/// (fire-and-forget). Failures are logged and do not propagate to the execution pipeline.
		/// </summary>
		/// <param name="context">The context of the finished execution.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		private Task RunExecutionFinishedHooksAsync(CancellationToken cancellationToken)
		{
			foreach (var hook in _executionHooks)
			{
				try
				{
					_ = hook.OnExecutionFinishedAsync(chat, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Hook {Hook} failed in OnExecutionFinished: {Error}", hook.GetType().Name, ex.Message);
				}
			}

			return Task.CompletedTask;
		}

		private void RecordFailedUsage(string modelName, DateTime timeRequested, string errorMessage)
		{
			try
			{
				usageStatsCollector.RecordUsage(
					model: modelName,
					inputTokens: 0,
					outputTokens: 0,
					durationMs: (long)(DateTime.Now - timeRequested).TotalMilliseconds,
					success: false,
					errorMessage: errorMessage);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to record usage statistics for failed request");
			}
		}
	}
}