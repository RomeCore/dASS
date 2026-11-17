using System.Diagnostics;
using System.Text;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Hooks.Chat;
using LLMDesktopAssistant.Hooks.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Completions;
using RCLargeLanguageModels.Messages;
using RCLargeLanguageModels.Messages.Attachments;
using RCLargeLanguageModels.Metadata;
using RCLargeLanguageModels.Tools;
using Serilog;
using IToolDescriptor = LLMDesktopAssistant.Tools.Execution.Domain.IToolDescriptor;
using ToolInvocationRequest = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationRequest;
using ToolInvocationSource = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationSource;
using ToolPolicyMask = LLMDesktopAssistant.Tools.ToolPolicyMask;
using ToolStatus = LLMDesktopAssistant.LLM.Domain.ToolStatus;

namespace LLMDesktopAssistant.Agents.Tasks
{
	[Service(typeof(IAgentTaskExecutor))]
	public class AgentTaskExecutor : IAgentTaskExecutor
	{
		private readonly AsyncLocal<AgentTask?> _currentTask = new();
		private readonly IModelManager _modelManager;
		private readonly IToolMemorizationService _toolMemorizationService;
		private readonly IUsageStatsCollector _usageStatsCollector;
		private readonly IAgentTaskDispatcher _dispatcher;
		private readonly IToolInvocationDispatcher _toolDispatcher;
		private readonly IServiceProvider _services;
		private readonly IMemoryFactStore _memoryFactStore;
		private readonly IMemoryLogStore _memoryLogStore;

		public AgentTask? Current => _currentTask.Value;

		public AgentTaskExecutor(IModelManager modelManager,
			IToolMemorizationService toolMemorizationService, IUsageStatsCollector usageStatsCollector, IAgentTaskDispatcher dispatcher,
			IToolInvocationDispatcher toolDispatcher, IServiceProvider services,
			IMemoryFactStore memoryFactStore, IMemoryLogStore memoryLogStore)
		{
			_modelManager = modelManager;
			_toolMemorizationService = toolMemorizationService;
			_usageStatsCollector = usageStatsCollector;
			_dispatcher = dispatcher;
			_toolDispatcher = toolDispatcher;
			_services = services;
			_memoryFactStore = memoryFactStore;
			_memoryLogStore = memoryLogStore;
		}

		private static IExecutionInfoStatusService? GetExecutionStatusService(AgentTask task)
		{
			var chat = task.LaunchParameters.TriggeredChat;
			return chat?.Services.GetService<IExecutionInfoStatusService>();
		}

		public AgentTask Execute(AgentTaskLaunchParameters parameters, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(parameters.ModelName) && parameters.Model == null)
				throw new ArgumentException("Model name or model must be provided.");
			if (parameters.InitialMessages.IsEmpty)
				throw new ArgumentException("Initial messages must be provided.", nameof(parameters.InitialMessages));
			if (parameters.MaxParallelToolCalls < 1)
				throw new ArgumentException("Max parallel tool calls must be greater than 0.", nameof(parameters.MaxParallelToolCalls));
			if (!Enum.IsDefined(parameters.Behaviour))
				throw new ArgumentException("Invalid behaviour value.", nameof(parameters.Behaviour));

			if (parameters.FeedbackFunc != null && parameters.Behaviour != AgentTaskExecutionBehaviour.Normal)
				throw new ArgumentException("Feedback function is only supported for the normal execution behaviour.",
					nameof(parameters.FeedbackFunc));

			var completionSource = new TaskCompletionSource<AgentTask>();
			var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (parameters.TimeOut is { } timeout && timeout > TimeSpan.Zero)
				cts.CancelAfter(timeout);

			var parentTask = _currentTask.Value;
			var task = new AgentTask
			{
				Id = Guid.NewGuid(),
				Executor = this,
				Parent = parentTask,
				LaunchParameters = parameters,
				Completion = completionSource.Task,
				CancellationTokenSource = cts
			};

			var model = parameters.Model ?? _modelManager.GetModel(parameters.ModelName!);

			var agentTools = parameters.Tools.ToList();
			string? additionalPrompt = null;
			var additionalPromptParts = new StringBuilder();
			if (parameters.Skills.Count > 0)
			{
				agentTools.Add(new SkillLoadTool
				{
					Skills = parameters.Skills.ToImmutableDictionary(s => s.Name),
					ApprovalLevel = ToolApprovalLevel.AlwaysApprove
				});

				var sb = new StringBuilder();
				foreach (var skill in parameters.Skills)
				{
					sb.AppendLine("\t<skill>");
					sb.AppendLine($"\t\t<name>{skill.Name}</name>");
					sb.AppendLine($"\t\t<description>{skill.Description}</description>");
					if (!string.IsNullOrEmpty(skill.Path))
						sb.AppendLine($"\t\t<path>{skill.Path}</path>");
					sb.AppendLine("\t</skill>");
				}

				string skillPrompt = $"""
					<available_skills>
						The following skills provide specialized instructions for specific tasks.
						When a task matches a skill's description, call the `skill-load` tool
						with the skill's name to load its full instructions.
					{sb}
					</available_skills>
					""";

				additionalPromptParts.AppendLine(skillPrompt);
			}

			if (parameters.MemoryBlocks.Count > 0)
			{
				var readableFactBlocks = parameters.MemoryBlocks.Where(b => b.CanRead && b.Block.FactsEnabled).Select(b => b.Block).ToList();
				var writableFactBlocks = parameters.MemoryBlocks.Where(b => b.CanWrite && b.Block.FactsEnabled).Select(b => b.Block).ToList();
				var readableLogBlocks = parameters.MemoryBlocks.Where(b => b.CanRead && b.Block.LogsEnabled).Select(b => b.Block).ToList();
				var writableLogBlocks = parameters.MemoryBlocks.Where(b => b.CanWrite && b.Block.LogsEnabled).Select(b => b.Block).ToList();

				agentTools.AddRange(new AgentMemoryTools(_memoryFactStore, _memoryLogStore, 0,
					readableFactBlocks, writableFactBlocks, readableLogBlocks, writableLogBlocks, 0).CreateManualTools());

				var sb = new StringBuilder();
				foreach (var block in parameters.MemoryBlocks)
				{
					sb.AppendLine("\t<memory_block>");
					sb.AppendLine($"\t\t<name>{block.Block.Name}</name>");
					if (!string.IsNullOrEmpty(block.Block.Description))
						sb.AppendLine($"\t\t<description>{block.Block.Description}</description>");
					if (block.CanRead && block.CanWrite)
						sb.AppendLine("\t\t<access>read-write</access>");
					else if (block.CanRead)
						sb.AppendLine("\t\t<access>read-only</access>");
					else if (block.CanWrite)
						sb.AppendLine("\t\t<access>write-only</access>");
					else
						throw new InvalidOperationException("Memory block must have at least one access mode.");
					if (block.Block.FactsEnabled && block.Block.LogsEnabled)
						sb.AppendLine("\t\t<types>facts,logs</types>");
					else if (block.Block.FactsEnabled)
						sb.AppendLine("\t\t<types>facts</types>");
					else if (block.Block.LogsEnabled)
						sb.AppendLine("\t\t<types>logs</types>");
					else
						sb.AppendLine("\t\t<types>none</types>");
					sb.AppendLine("\t</memory_block>");
				}

				additionalPromptParts.AppendLine($"""
					<available_memory_blocks>
						The following memory blocks are accessible via the provided memory tools.
						Search tools are available for readable blocks and write tools for writable blocks.
						Use them to read or update the assistant's persistent memory when the task requires it.
					{sb}
					</available_memory_blocks>
					""");
			}

			if (parameters.SubAgents.Count > 0)
			{
				if (parameters.TriggeredChat is null)
					throw new ArgumentException("Sub-agents can only be used when called from a chat.", nameof(parameters.TriggeredChat));

				var resolver = parameters.TriggeredChat.Services.GetRequiredService<ISubAgentTaskParamsResolver>();

				agentTools.AddRange(new AgentSubAgentTools(this, resolver, parameters, parameters.SubAgents)
					.CreateSubAgentCallTools());

				var sb = new StringBuilder();
				foreach (var subAgent in parameters.SubAgents)
				{
					sb.AppendLine("\t<sub_agent>");
					sb.AppendLine($"\t\t<name>{subAgent.Name}</name>");
					sb.AppendLine($"\t\t<description>{subAgent.Description}</description>");
					sb.AppendLine("\t</sub_agent>");
				}

				additionalPromptParts.AppendLine($"""
					<sub_agents>
						The following sub-agents are available to assist with specific tasks.
						If a task matches a sub-agent's description, call the `agent-callsub` tool
					{sb}
					</sub_agents>
					""");
			}

			if (additionalPromptParts.Length > 0)
				additionalPrompt = additionalPromptParts.ToString();

			agentTools = agentTools.GroupBy(t => t.Name).Select(t => t.Last()).ToList();
			var tools = agentTools.ToImmutableDictionary(k => k.Name);

			model = model.WithTools(agentTools.Select(t => new FunctionTool(t.Name, t.Description, t.ArgumentSchema,
				(_, _) => throw new Exception("This tool is not expected to be invoked directly."))));

			task.Messages.AddRange(parameters.InitialMessages);
			task.InitialMessageCount = task.Messages.Count;
			var nativeMessages = new List<IMessage>();

			int startIndex = 0;
			if (task.Messages[0] is AgentSystemMessage systemMessage)
			{
				if (!string.IsNullOrEmpty(additionalPrompt))
				{
					if (string.IsNullOrEmpty(systemMessage.Content))
						nativeMessages.Add(new SystemMessage(additionalPrompt));
					else
						nativeMessages.Add(new SystemMessage($"""
							{systemMessage.Content}

							{additionalPrompt}
							"""));
				}
				else
				{
					nativeMessages.Add(new SystemMessage(systemMessage.Content ?? string.Empty));
				}
				startIndex = 1;
			}
			for (int i = startIndex; i < task.Messages.Count; i++)
				nativeMessages.AddRange(ConvertMessageFromAgent(task.Messages[i]));

			var executionInfo = new AgentTaskExecutionInfo
			{
				Model = model,
				Tools = tools,
				NativeMessages = nativeMessages
			};
			task.ExecutionInfo = executionInfo;

			Launch(task, completionSource, executionInfo, isContinue: false);

			return task;
		}

		public void Continue(AgentTask task, IEnumerable<AgentChatMessage> messages, CancellationToken cancellationToken = default)
		{
			if (!task.Completed)
				throw new InvalidOperationException("Cannot continue a task that has not completed. " +
					"Use AgentTask.FollowUpMessages to add messages during working task.");

			if (task.ExecutionInfo is not { } executionInfo)
				throw new InvalidOperationException("Cannot continue a task that is likely was not executed using this executor.");

			foreach (var message in messages)
				task.FollowUpMessages.Enqueue(message);

			var completionSource = new TaskCompletionSource<AgentTask>();
			var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			task.Completed = false;
			task.Completion = completionSource.Task;
			task.CancellationTokenSource = cts;
			task.Exception = null;

			Launch(task, completionSource, executionInfo, isContinue: true);
		}

		void Launch(AgentTask task, TaskCompletionSource<AgentTask> completionSource,
			AgentTaskExecutionInfo executionInfo, bool isContinue)
		{
			task.CompletedAt = null;
			task.BlockReason = null;

			if (!isContinue)
				task.StartedAt = DateTime.Now;
			task.ContinuedAt = DateTime.Now;
			task.ContinuedMessageCount = task.Messages.Count;

			Task.Run(async () =>
			{
				task.Status = AgentTaskStatus.Executing;
				task.NewUsageStatistics = null;
				_currentTask.Value = task;
				_toolMemorizationService.PushTaskAsyncScope();
				_dispatcher.OnBeginTask(task);

				using var execution = GetExecutionStatusService(task)?.WithExecution();

				try
				{
					if (await GetBlockReasonAsync(task, isContinue, task.ContinuedAt) is { } blockReason)
					{
						task.BlockReason = blockReason;
						task.Completed = true;
						task.CompletedAt = DateTime.Now;
						task.Status = AgentTaskStatus.Blocked;
						task.Exception = new AgentTaskBlockedException(blockReason);
						completionSource.SetException(task.Exception);
						return;
					}

					await ExecuteAsync(task, executionInfo);

					task.Completed = true;
					task.CompletedAt = DateTime.Now;
					task.Status = AgentTaskStatus.Success;
					completionSource.SetResult(task);
				}
				catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is OperationCanceledException))
				{
					task.Completed = true;
					task.CompletedAt = DateTime.Now;
					task.Status = AgentTaskStatus.Cancelled;
					completionSource.SetCanceled();
				}
				catch (OperationCanceledException)
				{
					task.Completed = true;
					task.CompletedAt = DateTime.Now;
					task.Status = AgentTaskStatus.Cancelled;
					completionSource.SetCanceled();
				}
				catch (Exception ex)
				{
					task.Completed = true;
					task.CompletedAt = DateTime.Now;
					task.Status = AgentTaskStatus.Failed;
					task.Exception = ex;
					completionSource.SetException(ex);
				}
				finally
				{
					_dispatcher.OnEndTask(task);
					FireStopHooks(task, isContinue, task.ContinuedAt);
				}
			}, CancellationToken.None);
		}

		/// <summary>
		/// Gets the reason a <c>task.start</c> hook refused the launch, or <see langword="null"/> when it may run.
		/// </summary>
		/// <param name="task">The task being launched.</param>
		/// <param name="isContinue">Whether this launch continues a finished task.</param>
		/// <param name="launchStartedAt">The moment this launch started.</param>
		private async Task<string?> GetBlockReasonAsync(AgentTask task, bool isContinue, DateTime launchStartedAt)
		{
			if (ResolveTaskHooks(task) is not { } hooks)
				return null;

			var result = await hooks.Dispatcher.ExecuteHooksAsync<TaskStartHookVerdict>(
				BuildPayload(task, hooks, isContinue, launchStartedAt),
				TaskHookEvents.ComposeStart(task.LaunchParameters.Source, isContinue),
				DescribeTarget(task), HookExecutionFlow.Sequential,
				cancellationToken: task.CancellationTokenSource.Token);

			// A hook that broke the chain itself - a blocking second return in Lua - refuses the launch outright.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
				return result.BlockingMessage;

			if (result.Value is not { Decision: ChatHookDecision.Disallow or ChatHookDecision.Error } verdict)
				return null;

			return string.IsNullOrWhiteSpace(verdict.Reason)
				? LocalizationManager.LocalizeStatic("task.hook.blocked")
				: verdict.Reason;
		}

		/// <summary>
		/// Fires <c>task.stop</c> for the launch that has just ended, in the current terminal status of the task.
		/// </summary>
		/// <param name="task">The task that ended.</param>
		/// <param name="isContinue">Whether the launch that ended was a continuation.</param>
		/// <param name="launchStartedAt">The moment the launch that ended started.</param>
		private void FireStopHooks(AgentTask task, bool isContinue, DateTime launchStartedAt)
		{
			if (ResolveTaskHooks(task) is not { } hooks)
				return;

			hooks.Dispatcher.ExecuteHooksFnF(BuildPayload(task, hooks, isContinue, launchStartedAt),
				TaskHookEvents.ComposeStop(task.LaunchParameters.Source, task.Status), DescribeTarget(task));
		}

		private sealed record TaskHookSurface(IHookDispatcher Dispatcher, int? ChatId);

		private TaskHookSurface? ResolveTaskHooks(AgentTask task)
		{
			if (task.LaunchParameters.TriggeredChat is not { } chat)
				return null;

			try
			{
				if (chat.Services.GetService<IHookDispatcher>() is not { } dispatcher)
					return null;

				return new TaskHookSurface(dispatcher, chat.Services.GetService<IChatCreationConfig>()?.ChatId);
			}
			catch (ObjectDisposedException)
			{
				Log.Debug("The chat of task {TaskId} was closed; its hooks are skipped.", task.Id);
				return null;
			}
		}

		private static string DescribeTarget(AgentTask task) =>
			TaskHookEvents.ComposeTarget(task.LaunchParameters.SourceId, task.LaunchParameters.TaskName);

		private static TaskHookPayload BuildPayload(AgentTask task, TaskHookSurface hooks, bool isContinue,
			DateTime launchStartedAt)
		{
			var parameters = task.LaunchParameters;
			var isTerminal = task.Status is not (AgentTaskStatus.Pending or AgentTaskStatus.Executing);

			return new TaskHookPayload
			{
				TaskId = task.Id,
				ParentTaskId = task.Parent?.Id,
				TaskName = parameters.TaskName,
				Source = TaskHookEvents.DescribeSource(parameters.Source),
				SourceId = parameters.SourceId,
				ChatId = hooks.ChatId,
				Hidden = parameters.Hidden,
				TriggeredSender = parameters.TriggeredMessage?.Sender,
				Model = task.ExecutionInfo?.Model.Descriptor.FullName ?? parameters.ModelName,
				Behaviour = KebabEnumNames<AgentTaskExecutionBehaviour>.Serialize(parameters.Behaviour),
				Launch = isContinue ? "continue" : "new",
				Status = isTerminal ? TaskHookEvents.DescribeStatus(task.Status) : null,
				BlockReason = task.BlockReason,
				Error = task.Exception?.Message,
				ErrorType = task.Exception?.GetType().Name,
				MessageCount = task.Messages.Count,
				InitialMessageCount = task.InitialMessageCount,
				FeedbackIterations = task.FeedbackIterations,
				SubTaskCount = task.SubTasks.Count,
				LastGeneratedContent = task.LastGeneratedContent,
				Usage = task.UsageStatistics,
				NewUsage = task.NewUsageStatistics,
				StartedAt = launchStartedAt,
				CompletedAt = task.CompletedAt,
				DurationMs = task.CompletedAt is { } completedAt
					? (completedAt - launchStartedAt).TotalMilliseconds
					: null
			};
		}

		private async Task ExecuteAsync(AgentTask task, AgentTaskExecutionInfo info)
		{
			var semaphore = new SemaphoreSlim(task.LaunchParameters.MaxParallelToolCalls, task.LaunchParameters.MaxParallelToolCalls);

			while (true)
			{
				Stopwatch messageExecutionTimer = new(), inferenceTimer = new();
				messageExecutionTimer.Start();
				inferenceTimer.Start();

				try
				{
					var cancellationToken = task.CancellationTokenSource.Token;
					cancellationToken.ThrowIfCancellationRequested();

					while (task.FollowUpMessages.TryDequeue(out var followUpMessage))
					{
						var nativeFollowUp = ConvertMessageFromAgent(followUpMessage);
						info.NativeMessages.AddRange(nativeFollowUp);
						task.Messages.Add(followUpMessage);
					}

					TimeSpan? ttft = null;
					TimeSpan inferenceTime = TimeSpan.Zero;

					var response = await info.Model.ChatStreamingAsync(info.NativeMessages,
						cancellationToken: cancellationToken);

					cancellationToken.ThrowIfCancellationRequested();

					IAssistantMessage responseMessage = response.Message;
					info.NativeMessages.Add(responseMessage);

					var agentMessage = new AgentAssistantMessage
					{
						ReasoningContent = responseMessage.ReasoningContent,
						Content = responseMessage.Content
					};
					agentMessage.Attachments.AddRange(responseMessage.Attachments
						.Select(a => AgentNativeAttachment.TryFromNative(a)).Where(a => a != null)!);

					async Task<IToolMessage> ProcessToolCall(IToolCall toolCall)
					{
						// Do not execute tool if the task is configured to only respond.
						if (task.LaunchParameters.Behaviour is AgentTaskExecutionBehaviour.OnlyResponse)
							return new ToolMessage(string.Empty, toolCall.Id, toolCall.ToolName);

						await semaphore.WaitAsync(cancellationToken);
						try
						{
							return await ExecuteToolAsync(toolCall, agentMessage, task, info.Tools, cancellationToken);
						}
						finally
						{
							semaphore.Release();
						}
					}
					List<Task<IToolMessage>> toolCallTasks = [];

					for (int i = 0; i < responseMessage.ToolCalls.Count; i++)
						toolCallTasks.Add(ProcessToolCall(responseMessage.ToolCalls[i]));

					task.Messages.Add(agentMessage);
					task.LastGeneratedMessage = agentMessage;
					task.LastGeneratedContent = agentMessage.Content;

					if (responseMessage is PartialAssistantMessage partialResponseMessage)
					{
						void PartAdded(object? sender, AssistantMessageDelta delta)
						{
							ttft ??= messageExecutionTimer.Elapsed;

							if (delta.DeltaReasoningContent != null)
								agentMessage.ReasoningContent = responseMessage.ReasoningContent;

							if (delta.DeltaContent != null)
							{
								agentMessage.Content = responseMessage.Content;
								task.LastGeneratedContent = responseMessage.Content;
							}

							if (delta.NewAttachments != null)
								agentMessage.Attachments.AddRange(delta.NewAttachments
									.Select(a => AgentNativeAttachment.TryFromNative(a)).Where(a => a != null)!);

							if (delta.NewToolCalls != null)
								foreach (var toolCall in delta.NewToolCalls)
									toolCallTasks.Add(ProcessToolCall(toolCall));
						}
						partialResponseMessage.PartAdded += PartAdded;

						try
						{
							inferenceTimer.Restart();
							await partialResponseMessage;
							if (response is PartialChatCompletionResult partialResponse)
								await partialResponse;
							inferenceTime = inferenceTimer.Elapsed;
						}
						finally
						{
							partialResponseMessage.PartAdded -= PartAdded;
						}
					}

					ttft ??= TimeSpan.Zero;
					inferenceTime = inferenceTimer.Elapsed;
					inferenceTimer.Restart();

					int inputTokens = 0, outputTokens = 0;
					int cacheCreateTokens = 0, cacheHitTokens = 0, cacheMissTokens = 0;

					if (response.UsageMetadata is IUsageMetadata usageMetadata)
					{
						inputTokens = usageMetadata.InputTokens;
						outputTokens = usageMetadata.OutputTokens;

						if (usageMetadata is IUsageCacheMetadata cacheMetadata)
						{
							cacheCreateTokens = cacheMetadata.CachedTokens;
							cacheHitTokens = cacheMetadata.InputCacheHitTokens;
							cacheMissTokens = cacheMetadata.InputCacheMissTokens;

							if (cacheCreateTokens < 0) cacheCreateTokens = 0;
							if (cacheHitTokens < 0) cacheHitTokens = 0;
							if (cacheMissTokens < 0) cacheMissTokens = 0;
						}

						if (inputTokens < 0) inputTokens = 0;
						if (outputTokens < 0) outputTokens = 0;
					}

					agentMessage.UsageStatistics = new AgentUsageStatistics
					{
						InputTokens = inputTokens,
						OutputTokens = outputTokens,
						CacheCreateTokens = cacheCreateTokens,
						InputCacheHitTokens = cacheHitTokens,
						InputCacheMissTokens = cacheMissTokens,
						TimeToFirstToken = ttft.Value,
						InferenceTime = inferenceTime,
						ExecutionTime = messageExecutionTimer.Elapsed
					};

					if (task.UsageStatistics is { } prevStats)
					{
						task.UsageStatistics = new AgentUsageStatistics
						{
							InputTokens = prevStats.InputTokens + inputTokens,
							OutputTokens = prevStats.OutputTokens + outputTokens,
							CacheCreateTokens = prevStats.CacheCreateTokens + cacheCreateTokens,
							InputCacheHitTokens = prevStats.InputCacheHitTokens + cacheHitTokens,
							InputCacheMissTokens = prevStats.InputCacheMissTokens + cacheMissTokens,
							TimeToFirstToken = prevStats.TimeToFirstToken + ttft.Value,
							InferenceTime = prevStats.InferenceTime + inferenceTime,
							ExecutionTime = prevStats.ExecutionTime + messageExecutionTimer.Elapsed
						};
					}
					else
					{
						task.UsageStatistics = agentMessage.UsageStatistics;
					}

					if (task.NewUsageStatistics is { } prevNewStats)
					{
						task.NewUsageStatistics = new AgentUsageStatistics
						{
							InputTokens = prevNewStats.InputTokens + inputTokens,
							OutputTokens = prevNewStats.OutputTokens + outputTokens,
							CacheCreateTokens = prevNewStats.CacheCreateTokens + cacheCreateTokens,
							InputCacheHitTokens = prevNewStats.InputCacheHitTokens + cacheHitTokens,
							InputCacheMissTokens = prevNewStats.InputCacheMissTokens + cacheMissTokens,
							TimeToFirstToken = prevNewStats.TimeToFirstToken + ttft.Value,
							InferenceTime = prevNewStats.InferenceTime + inferenceTime,
							ExecutionTime = prevNewStats.ExecutionTime + messageExecutionTimer.Elapsed
						};
					}
					else
					{
						task.NewUsageStatistics = agentMessage.UsageStatistics;
					}

					_usageStatsCollector.RecordUsage(info.Model.Descriptor.FullName,
						inputTokens, outputTokens,
						cacheHitTokens, cacheMissTokens,
						messageExecutionTimer.ElapsedMilliseconds,
						success: true);

					info.NativeMessages.AddRange(await Task.WhenAll(toolCallTasks));

					if (task.LaunchParameters.Behaviour
						is AgentTaskExecutionBehaviour.ExecuteOnce or AgentTaskExecutionBehaviour.OnlyResponse)
						break;

					if (agentMessage.ToolCalls.Count == 0 && task.FollowUpMessages.IsEmpty)
					{
						cancellationToken.ThrowIfCancellationRequested();

						if (task.LaunchParameters.FeedbackFunc is not null)
						{
							var feedbackMessage = await task.LaunchParameters.FeedbackFunc.Invoke(task, cancellationToken);
							if (feedbackMessage != null)
							{
								task.Messages.Add(feedbackMessage);
								info.NativeMessages.Add(new UserMessage(Senders.User, feedbackMessage.Content ?? string.Empty,
									ConvertAttachments(feedbackMessage.Attachments)));
								task.FeedbackIterations++;
								continue;
							}
						}

						break;
					}
				}
				catch (Exception ex)
				{
					_usageStatsCollector.RecordUsage(info.Model.Descriptor.FullName,
						0, 0,
						0, 0,
						messageExecutionTimer.ElapsedMilliseconds,
						success: false,
						ex.Message);
					throw;
				}
				finally
				{
					messageExecutionTimer.Stop();
					inferenceTimer.Stop();
				}
			}
		}

		private async Task<IToolMessage> ExecuteToolAsync(IToolCall toolCall, AgentAssistantMessage message,
			AgentTask task, ImmutableDictionary<string, IToolDescriptor> tools, CancellationToken cancellationToken = default)
		{
			if (toolCall is not IFunctionToolCall functionCall)
				throw new ArgumentException($"Tool call '{toolCall}' is not a function tool call.");

			var agentToolCall = new AgentToolCall
			{
				Status = ToolStatus.Pending,
				ToolCallId = toolCall.Id,
				ToolName = toolCall.ToolName,
				Arguments = functionCall.Args,
				Result = null
			};
			message.ToolCalls.Add(agentToolCall);

			if (functionCall is PartialFunctionToolCall partialFunctionCall)
			{
				void ArgsPartAdded(object? sender, string e)
				{
					agentToolCall.Arguments = functionCall.Args;
				}
				partialFunctionCall.ArgsPartAdded += ArgsPartAdded;

				try
				{
					await partialFunctionCall;
				}
				finally
				{
					partialFunctionCall.ArgsPartAdded -= ArgsPartAdded;
				}
			}

			// The task sequence has no streaming stage: the drain above is entry-point plumbing.
			tools.TryGetValue(toolCall.ToolName, out var descriptor);

			var request = new ToolInvocationRequest
			{
				Source = ToolInvocationSource.AgentTask,
				Call = agentToolCall,
				Descriptor = descriptor ?? new Tools.Execution.Domain.MissingToolDescriptor(toolCall.ToolName),
				Confirmation = new TaskConfirmationSink(task, GetExecutionStatusService(task), _toolMemorizationService),
				ContextFactory = new TaskToolContextFactory(task.LaunchParameters.TriggeredChat,
					task.LaunchParameters.TriggeredChat?.Services ?? _services),
				Host = message,
				RunningInUi = false,
				EffectiveBehaviours = new ToolPolicyMask
				{
					AutoApproveBehaviours = task.LaunchParameters.AutoApproveBehaviours,
					DisallowedBehaviours = task.LaunchParameters.DisallowedBehaviours
				}
			};

			await _toolDispatcher.RunAsync(request, cancellationToken);

			agentToolCall.Result = new AgentToolCallResult
			{
				Success = agentToolCall.Status == ToolStatus.Success,
				Content = ToolCallContentBuilder.Build(agentToolCall.ResultContent, agentToolCall.Attachments),
				Attachments = agentToolCall.Attachments.ToImmutableList()
			};

			var resultStatus = agentToolCall.Status == ToolStatus.Success
				? ToolResultStatus.Success
				: ToolResultStatus.Error;
			return new ToolMessage(new ToolResult(resultStatus, agentToolCall.Result.Content,
				ConvertAttachments(agentToolCall.Result.Attachments)), toolCall.Id, toolCall.ToolName);
		}

		private static IEnumerable<IMessage> ConvertMessageFromAgent(AgentChatMessage agentMessage)
		{
			switch (agentMessage)
			{
				case AgentSystemMessage systemMessage:
					return [new SystemMessage(systemMessage.Content ?? string.Empty)];

				case AgentUserMessage userMessage:
					return [new UserMessage(Senders.User, userMessage.Content ?? string.Empty,
						ConvertAttachments(userMessage.Attachments))];

				case AgentAssistantMessage assistantMessage:

					var toolCalls = new List<IToolCall>();
					var toolMessages = new List<IMessage>();

					foreach (var toolCall in assistantMessage.ToolCalls)
					{
						toolCalls.Add(new FunctionToolCall(toolCall.ToolCallId, toolCall.ToolName, toolCall.Arguments));
						toolMessages.Add(new ToolMessage(ConvertToolResultFromAgent(toolCall.Result), toolCall.ToolCallId, toolCall.ToolName));
					}

					var nativeAssistantMessage = new AssistantMessage(assistantMessage.Content, assistantMessage.ReasoningContent,
						toolCalls, ConvertAttachments(assistantMessage.Attachments));

					return [nativeAssistantMessage, ..toolMessages];

				default:
					throw new ArgumentOutOfRangeException(nameof(agentMessage), $"Unknown message type: {agentMessage.GetType()}");
			}
		}

		private static ToolResult ConvertToolResultFromAgent(AgentToolCallResult? agentToolResult)
		{
			if (agentToolResult == null)
				return new ToolResult(ToolResultStatus.NoResult, "Tool call has given no result.");

			var status = agentToolResult.Success ? ToolResultStatus.Success : ToolResultStatus.Error;
			return new ToolResult(status, agentToolResult.Content, ConvertAttachments(agentToolResult.Attachments));
		}

		/// <summary>
		/// Projects task attachments onto the native attachments the model receives. Text contributions have
		/// no native form and are sent as content by the call's own projection instead.
		/// </summary>
		private static IEnumerable<IAttachment> ConvertAttachments(IEnumerable<AgentAttachment> attachments) =>
			attachments.OfType<INativeChatAttachment>()
				.Select(attachment => attachment.Native)
				.OfType<IAttachment>();
	}
}
