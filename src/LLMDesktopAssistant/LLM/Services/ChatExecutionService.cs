using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Controls.Toasts;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Hooks.Chat;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional.Context;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Messages;
using RCLargeLanguageModels.Metadata;
using RCLargeLanguageModels.Tools;
using Serilog;
using ToolInvocationRequest = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationRequest;
using ToolInvocationSource = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationSource;
using ToolPolicyMask = LLMDesktopAssistant.Tools.ToolPolicyMask;

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
		IToolInvocationDispatcher dispatcher,
		IToolMemorizationService toolMemorizer,
		IEnumerable<IChatExecutionHook> executionHooks,
		IAddonSetCollector<ToolInfo> toolsetCollector,
		IMCPManagementService mcpManager,
		IUsageStatsCollector usageStatsCollector,
		IToastService toastService,
		IExecutionInfoStatusService executionInfoStatusService,
		IChatExecutionStatusService executionStatusService,
		IChatStatusService statusService,
		IPromptDumpService promptDumpService,
		IFollowUpChatMessageQueue followUps,
		IHookDispatcher hookDispatcher
	) : IChatExecutionService
	{
		private const bool EnablePrefixCompletions = false;

		private readonly List<IChatExecutionHook> _executionHooks = executionHooks.OrderBy(h => h.Order).ToList();
		private readonly ChatConfirmationSink _confirmationSink = new(executionInfoStatusService);
		private readonly IToolExecutionContextFactory _chatContextFactory =
			new ChatToolContextFactory(chat, toolMemorizer, agentManager, chatSettings);

		/// <inheritdoc/>
		public async Task<bool> GenerateResponseAsync(CancellationToken cancellationToken = default)
		{
			using var executionInfo = executionInfoStatusService.WithExecution();
			using var execution = executionStatusService.WithExecution();

			// Hoisted for the exit paths: the turn stop hook fires once, from the finally, and only for a turn that
			// actually began.
			var token = cancellationToken;
			var agents = new List<ChatHookAgentInfo>();
			var cutByFollowUp = false;
			var started = false;

			try
			{
				int cycles = 0;
				using var scope = tokens.WithToken(ChatExecutionLevel.AgentSequence, cancellationToken, out token);

				if (!await RunTurnBeginHookAsync(token))
					return false;

				started = true;

				while (true)
				{
					token.ThrowIfCancellationRequested();

					var lastEntry = chat.Messages.LastOrDefault();
					var lastAssistantMessage = lastEntry?.Message as Domain.AssistantMessage;
					var continuesToolBatch = lastAssistantMessage != null && lastAssistantMessage.ToolCalls.Count != 0;

					Guid? nextAgentId = continuesToolBatch ? lastAssistantMessage!.SenderAgentId : null;
					Guid? agentStageId = continuesToolBatch ? lastAssistantMessage!.AgentStageId : null;

					if (!continuesToolBatch)
					{
						statusService.Icon = MaterialIconKind.RobotConfused;
						statusService.Text = LocalizationManager.LocalizeStatic("chat.status.selecting_agent");

						var agentTuple = await agentOrderer.GetNextAgentAsync(token);
						nextAgentId = agentTuple?.Item1;
						agentStageId = agentTuple?.Item2;
					}
					else if (nextAgentId is { } continuedAgentId && agentStageId is { } continuedStageId)
					{
						// No selection happens on this path, so the selection hook is skipped - the continuation hook is
						// what reports it. The cycle is unknown: the earlier ones belong to a previous turn.
						FireAgentContinueHook(lastEntry, continuedAgentId, continuedStageId, cycle: null,
							AgentContinueReason.PendingToolCalls, token);
					}

					if (nextAgentId == null || agentStageId == null)
					{
						if (cycles == 0)
							toastService.ShowWarning(LocalizationManager.LocalizeStatic("chat.toast.agent_selection_failed.title"),
								LocalizationManager.LocalizeStatic("chat.toast.agent_selection_failed.description"));
						else
							await RunExecutionFinishedHooksAsync(token);

						cutByFollowUp = followUps.HasEligible(FollowUpInjectionLevel.MultiAgentSequence);
						return cutByFollowUp;
					}

					token.ThrowIfCancellationRequested();

					if (agentManager.TryGetAgentDescriptor(nextAgentId.Value) is { } selectedAgent)
					{
						agents.Add(new ChatHookAgentInfo
						{
							AgentId = nextAgentId.Value,
							Agent = selectedAgent.Info.Name,
							StageId = agentStageId.Value
						});
					}

					if (await GenerateResponseWithAgentAsync(nextAgentId.Value, agentStageId.Value, token))
					{
						cutByFollowUp = true;
						return cutByFollowUp;
					}

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

				if (started)
					FireTurnStopHook(agents, cutByFollowUp, token);
			}
		}

		/// <inheritdoc/>
		public async Task<bool> GenerateResponseWithAgentAsync(Guid agentId, Guid agentStageId,
			CancellationToken cancellationToken = default)
		{
			using var execution = executionStatusService.WithExecution();

			// Hoisted for the exit paths: the agent stop hook fires once, from the finally, with what the run
			// produced and whether a follow-up cut it short.
			var token = cancellationToken;
			var agentName = string.Empty;
			Domain.AssistantMessage? lastResponse = null;
			BranchedMessage? lastEntry = null;
			List<int> messageIds = [];
			var producedMessages = 0;
			var cutByFollowUp = false;

			try
			{
				using var scope = tokens.WithToken(ChatExecutionLevel.Agent, cancellationToken, out token);
				var agent = agentManager.GetAgentDescriptor(agentId);
				agentName = agent.Info.Name;
				toolMemorizer.PushTaskAsyncScope();

				var responsesBuilder = ImmutableList.CreateBuilder<Domain.AssistantMessage>();
				int cycles = 0;

				while (true)
				{
					if (cycles > 0)
					{
						// The agent produced tool calls and continues with their results: no selection happens, so the
						// continuation hook reports this cycle.
						FireAgentContinueHook(lastEntry, agentId, agentStageId, cycle: cycles,
							AgentContinueReason.ToolCycle, token);
					}

					var (response, entry, interrupted) = await GenerateMessageResponseAsync(agentId, agentStageId, agent, cycles, token);
					lastResponse = response;
					lastEntry = entry;
					producedMessages++;

					if (entry is { MessageId: >= 0 })
						messageIds.Add(entry.MessageId);

					// The message barrier fired: the message is committed, the sequence ends here.
					if (interrupted)
					{
						cutByFollowUp = true;
						return true;
					}

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

						// The agent barrier sits after the hooks: the agent finished its cycle honestly.
						cutByFollowUp = followUps.HasEligible(FollowUpInjectionLevel.Agent);
						return cutByFollowUp;
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

				FireAgentStopHook(agentId, agentName, agentStageId, lastResponse, producedMessages, messageIds,
					cutByFollowUp, token);
			}
		}

		private async Task<(Domain.AssistantMessage Message, BranchedMessage? Entry, bool Interrupted)>
			GenerateMessageResponseAsync(Guid agentId, Guid agentStageId, ChatAgentDescriptor agent, int cycle,
			CancellationToken cancellationToken = default)
		{
			using var execution = executionStatusService.WithExecution();

			// Hoisted for the exit paths: the stop hook fires once, from the finally, with the status the message
			// ended with, and the ambient message window needs the history entry the write produced.
			Domain.AssistantMessage domainResponseMessage = null!;
			BranchedMessage? branchedMessage = null;
			var token = cancellationToken;

			try
			{
				using var scope = tokens.WithToken(ChatExecutionLevel.Message, cancellationToken, out token);
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

				domainResponseMessage = new Domain.AssistantMessage
				{
					CreatedAt = DateTime.Now,
					Status = AssistantMessageStatus.Pending,
					SenderAgentId = agentId,
					Sender = agent.Info.Name,
					AgentStageId = agentStageId,
					IsUserLike = agent.Info.IdentifyAsUser
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

				branchedMessage = chat.Messages[^1];
				LuaExecutionContext.SetMessage(branchedMessage);
				await RunMessageStartHookAsync(branchedMessage, agentId, agentStageId, agent, cycle, modelName, token);

				var timeRequested = DateTime.Now;
				DateTime? timeFirstToken = null;

				await RunResponsePrepareHooksAsync(new ChatPrepareExecutionHookContext
				{
					Chat = chat,
					Agent = agent,
					Response = domainResponseMessage,
					Cycle = cycle
				}, token);

				var promptBundle = await promptComposer.BuildAsync(agent, token);
				var inputMessages = promptBundle.Messages;
				var toolset = promptBundle.Tools;

				statusService.Icon = MaterialIconKind.ChatProcessing;
				statusService.Text = LocalizationManager.LocalizeStatic("chat.status.waiting_for_first_response");

				var response = await llm.ChatStreamingAsync(inputMessages, tools: toolset, cancellationToken: token);
				var responseMessage = response.Message;

				List<Task> toolExecutionTasks = [];
				var lockObj = new object();

				var toolPolicy = agent.Tools.GetEffectivePolicy(chatSettings.Settings);
				var effectiveBehaviours = new ToolPolicyMask
				{
					AutoApproveBehaviours = toolPolicy.AutoApproveBehaviours,
					DisallowedBehaviours = toolPolicy.DisallowedBehaviours
				};

				void ProcessToolCall(IToolCall toolCall)
				{
					if (toolCall is not IFunctionToolCall funtionCall)
						throw new InvalidOperationException($"Unsupported tool call type: {toolCall.GetType()}.");

					var toolInfo = toolsetCollector.GetAddonsForAgent(agent).GetByName(toolCall.ToolName);
					if (toolInfo is not null && toolInfo.Name != toolCall.ToolName)
					{
						Log.Information($"Tool call '{toolCall.ToolName}' is aliased as '{toolInfo.Name}'. Using the alias.");
					}

					var domainToolCall = new Domain.ToolCall
					{
						Status = ToolStatus.None,
						ToolCallId = toolCall.Id,
						ToolName = toolInfo?.Name ?? toolCall.ToolName,
						Title = toolInfo?.NameKey,
						Arguments = funtionCall.Args
					};
					domainToolCall.CanBeCompacted = toolInfo?.CanBeCompacted ?? false;
					domainResponseMessage.ToolCalls.Add(domainToolCall);

					var toolExecTask = dispatcher.RunAsync(new ToolInvocationRequest
					{
						Source = ToolInvocationSource.ChatRun,
						Call = domainToolCall,
						Descriptor = (LLMDesktopAssistant.Tools.Execution.Domain.IToolDescriptor?)toolInfo
							?? new LLMDesktopAssistant.Tools.Execution.Domain.MissingToolDescriptor(toolCall.ToolName),
						Confirmation = _confirmationSink,
						ContextFactory = toolInfo?.ContextFactory ?? _chatContextFactory,
						Host = domainResponseMessage,
						ArgumentStream = funtionCall is PartialFunctionToolCall partialToolCall
							? new PartialFunctionToolCallArgumentStream(partialToolCall)
							: null,
						EffectiveBehaviours = effectiveBehaviours,
						RunningInUi = true
					}, token);

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
									CacheCreateTokens = usageCacheMetadata.CachedTokens,
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
									CacheCreateTokens = null,
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
								CacheCreateTokens = null,
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
						token.ThrowIfCancellationRequested();
					}
				}

				// The tool batch is closed and the message is committed: this is the message barrier.
				return (domainResponseMessage, branchedMessage, followUps.HasEligible(FollowUpInjectionLevel.Message));
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
			finally
			{
				if (domainResponseMessage is not null)
					FireMessageStopHook(domainResponseMessage, branchedMessage, agentId, agentStageId, agent, cycle, token);
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

		private async Task RunMessageStartHookAsync(BranchedMessage? branched, Guid agentId, Guid agentStageId,
			ChatAgentDescriptor agent, int cycle, string model, CancellationToken cancellationToken)
		{
			var payload = new AssistantMessageStartHookPayload
			{
				ChatId = chat.Id,
				MessageId = branched?.MessageId,
				Index = branched?.MessageIndex + 1,
				AgentId = agentId,
				Agent = agent.Info.Name,
				StageId = agentStageId,
				Cycle = cycle,
				Model = model
			};

			await hookDispatcher.ExecuteHooksAsync<ChatHookVerdict>(payload, ChatHookEvents.AssistantStart,
				ChatHookEvents.ComposeAgentTarget(agentId, agent.Info.Name), HookExecutionFlow.Parallel,
				cancellationToken: cancellationToken);
		}

		private void FireMessageStopHook(Domain.AssistantMessage message, BranchedMessage? branched, Guid agentId,
			Guid agentStageId, ChatAgentDescriptor agent, int cycle, CancellationToken token)
		{
			var status = DescribeStopStatus(message.Status, token.IsCancellationRequested);

			var payload = new AssistantMessageStopHookPayload
			{
				ChatId = chat.Id,
				MessageId = branched?.MessageId,
				Index = branched?.MessageIndex + 1,
				AgentId = agentId,
				Agent = agent.Info.Name,
				StageId = agentStageId,
				Cycle = cycle,
				Status = KebabEnumNames<MessageStopStatus>.Serialize(status) ?? string.Empty,
				Content = message.Content,
				ToolCalls = message.ToolCalls.Count == 0 ? null : [.. message.ToolCalls.Select(DescribeToolCall)],
				Usage = DescribeUsage(message),
				Error = message.Error?.Value,
				IsUserLike = message.IsUserLike
			};

			hookDispatcher.ExecuteHooksFnF(payload, ChatHookEvents.ComposeAssistantStop(status),
				ChatHookEvents.ComposeAgentTarget(agentId, agent.Info.Name));
		}

		private static MessageStopStatus DescribeStopStatus(AssistantMessageStatus status, bool cancelled) => status switch
		{
			AssistantMessageStatus.Success => MessageStopStatus.Success,
			AssistantMessageStatus.Error => MessageStopStatus.Error,
			AssistantMessageStatus.Cancelled => MessageStopStatus.Cancelled,
			_ => cancelled ? MessageStopStatus.Cancelled : MessageStopStatus.Error
		};

		private static AssistantMessageStopHookPayload.ToolCallInfo DescribeToolCall(Domain.ToolCall call) => new()
		{
			Name = call.ToolName,
			Status = KebabEnumNames<ToolStatus>.Serialize(call.Status) ?? string.Empty
		};

		private static AssistantMessageStopHookPayload.UsageInfo? DescribeUsage(Domain.AssistantMessage message)
		{
			if (!message.AdditionalData.TryGet<TokenCostViewModel>(out var usage))
				return null;

			return new AssistantMessageStopHookPayload.UsageInfo
			{
				Model = usage.ModelName,
				InputTokens = usage.InputTokens,
				CacheCreateTokens = usage.CacheCreateTokens,
				InputCacheHitTokens = usage.InputCacheHitTokens,
				InputCacheMissTokens = usage.InputCacheMissTokens,
				OutputTokens = usage.OutputTokens,
				TimeToFirstToken = usage.TTFT,
				GenerationTime = usage.GenerationTime
			};
		}

		/// <summary>
		/// Asks the hooks whether the turn should start and reports whether they let it.
		/// </summary>
		/// <param name="cancellationToken">The turn's token.</param>
		private async Task<bool> RunTurnBeginHookAsync(CancellationToken cancellationToken)
		{
			var last = chat.Messages.LastOrDefault();
			var payload = new TurnBeginHookPayload
			{
				ChatId = chat.Id,
				LastMessage = last is null ? null : ChatHookMessageInfo.Create(last)
			};

			var result = await hookDispatcher.ExecuteHooksAsync<ChatHookVerdict>(payload, ChatHookEvents.TurnBegin,
				null, HookExecutionFlow.Sequential, cancellationToken: cancellationToken);

			// A hook that broke the chain itself - a blocking exit code - beats any verdict: there is none.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
			{
				Log.Warning("The turn was blocked with: {Reason}", result.BlockingMessage);
				toastService.ShowError(result.BlockingMessage);
				return false;
			}

			if (result.Value is not { } verdict)
				return true;

			if (verdict.Decision is not (ChatHookDecision.Disallow or ChatHookDecision.Error))
				return true;

			Log.Warning("The turn was refused by a hook: {Reason}", verdict.Reason ?? "no reason given");

			var fallbackKey = verdict.Decision is ChatHookDecision.Error
				? "chat.hook.turn.error"
				: "chat.hook.turn.disallowed";
			toastService.ShowError(string.IsNullOrWhiteSpace(verdict.Reason) ? Locale.Get(fallbackKey) : verdict.Reason);

			return false;
		}

		/// <summary>
		/// Announces that the turn ended, once, whatever the exit path was.
		/// </summary>
		/// <param name="agents">The agents the turn selected, in order.</param>
		/// <param name="cutByFollowUp">Whether a pending follow-up ended the turn early.</param>
		/// <param name="token">The turn's token.</param>
		private void FireTurnStopHook(List<ChatHookAgentInfo> agents, bool cutByFollowUp, CancellationToken token)
		{
			var reason = token.IsCancellationRequested ? TurnStopReason.Cancelled
				: cutByFollowUp ? TurnStopReason.Interrupted
				: TurnStopReason.Complete;

			var payload = new TurnStopHookPayload
			{
				ChatId = chat.Id,
				Reason = KebabEnumNames<TurnStopReason>.Serialize(reason) ?? string.Empty,
				Agents = [.. agents]
			};

			hookDispatcher.ExecuteHooksFnF(payload, ChatHookEvents.ComposeTurnStop(reason), null);
		}

		/// <summary>
		/// Announces that an agent runs again without a fresh selection.
		/// </summary>
		/// <param name="continuedEntry">The history entry being continued, when the caller has it.</param>
		/// <param name="agentId">The agent that continues.</param>
		/// <param name="stageId">The stage the agent runs in.</param>
		/// <param name="cycle">The cycle about to run, or <see langword="null"/> when it cannot be counted.</param>
		/// <param name="reason">Why the agent continues.</param>
		/// <param name="token">The run's token.</param>
		private void FireAgentContinueHook(BranchedMessage? continuedEntry, Guid agentId, Guid stageId,
			int? cycle, AgentContinueReason reason, CancellationToken token)
		{
			var agent = agentManager.TryGetAgentDescriptor(agentId)?.Info.Name ?? string.Empty;
			var pendingToolCalls = continuedEntry is not null && continuedEntry.Message.ToolCalls.Count != 0
				? continuedEntry.Message.ToolCalls.Select(call => call.ToolName).ToArray()
				: null;

			var payload = new AgentContinueHookPayload
			{
				ChatId = chat.Id,
				AgentId = agentId,
				Agent = agent,
				StageId = stageId,
				Cycle = cycle,
				Reason = KebabEnumNames<AgentContinueReason>.Serialize(reason) ?? string.Empty,
				PendingToolCalls = reason is AgentContinueReason.PendingToolCalls ? pendingToolCalls : null,
				LastMessageId = continuedEntry is { MessageId: >= 0 } ? continuedEntry.MessageId : null
			};

			hookDispatcher.ExecuteHooksFnF(payload, ChatHookEvents.TurnAgentContinue,
				ChatHookEvents.ComposeAgentTarget(agentId, agent));
		}

		/// <summary>
		/// Announces that an agent's turn ended, once, whatever the exit path was.
		/// </summary>
		/// <param name="agentId">The agent whose turn ended.</param>
		/// <param name="agent">The name of that agent.</param>
		/// <param name="stageId">The stage the agent ran in.</param>
		/// <param name="lastMessage">The last message the agent produced, if any.</param>
		/// <param name="producedMessages">How many messages the run produced.</param>
		/// <param name="messageIds">The database ids the run left behind.</param>
		/// <param name="cutByFollowUp">Whether a follow-up ended the agent early.</param>
		/// <param name="token">The run's token.</param>
		private void FireAgentStopHook(Guid agentId, string agent, Guid stageId, Domain.AssistantMessage? lastMessage,
			int producedMessages, List<int> messageIds, bool cutByFollowUp, CancellationToken token)
		{
			var status = DescribeStopStatus(lastMessage?.Status ?? AssistantMessageStatus.Pending,
				token.IsCancellationRequested);
			var reason = cutByFollowUp ? AgentStopReason.Interrupted : AgentStopReason.Complete;

			var payload = new AgentStopHookPayload
			{
				ChatId = chat.Id,
				AgentId = agentId,
				Agent = agent,
				StageId = stageId,
				Status = KebabEnumNames<MessageStopStatus>.Serialize(status) ?? string.Empty,
				Reason = KebabEnumNames<AgentStopReason>.Serialize(reason) ?? string.Empty,
				Cycles = producedMessages,
				MessageIds = messageIds.Count == 0 ? null : [.. messageIds]
			};

			hookDispatcher.ExecuteHooksFnF(payload, ChatHookEvents.ComposeTurnAgentStop(status, reason),
				ChatHookEvents.ComposeAgentTarget(agentId, agent));
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