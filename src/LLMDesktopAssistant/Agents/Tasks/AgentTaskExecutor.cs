using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using LLMDesktopAssistant.Tools.Execution.Support;
using IToolDescriptor = LLMDesktopAssistant.Tools.Execution.Domain.IToolDescriptor;
using ToolInvocationRequest = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationRequest;
using ToolInvocationSource = LLMDesktopAssistant.Tools.Execution.Domain.ToolInvocationSource;
using ToolPolicyMask = LLMDesktopAssistant.Tools.ToolPolicyMask;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels;
using RCLargeLanguageModels.Completions;
using RCLargeLanguageModels.Messages;
using RCLargeLanguageModels.Messages.Attachments;
using RCLargeLanguageModels.Metadata;
using RCLargeLanguageModels.Tools;
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

		private static IChatExecutionStatusService? GetExecutionStatusService(AgentTask task)
		{
			var chat = task.LaunchParameters.TriggeredChat;
			return chat?.Services.GetService<IChatExecutionStatusService>();
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
			var nativeMessages = new List<IMessage>();
			foreach (var message in task.Messages)
				nativeMessages.AddRange(ConvertMessageFromAgent(message, ref additionalPrompt));
			if (additionalPrompt != null)
				nativeMessages.Insert(0, new SystemMessage(additionalPrompt));

			Task.Run(async () =>
			{
				task.Status = AgentTaskStatus.Executing;
				_currentTask.Value = task;
				_toolMemorizationService.PushTaskAsyncScope();
				_dispatcher.OnBeginTask(task);

				using var execution = GetExecutionStatusService(task)?.WithExecution();

				try
				{
					await ExecuteAsync(task, model, tools, nativeMessages);
					task.Status = AgentTaskStatus.Success;
					completionSource.SetResult(task);
				}
				catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is OperationCanceledException))
				{
					task.Status = AgentTaskStatus.Cancelled;
					completionSource.SetCanceled(cancellationToken);
				}
				catch (OperationCanceledException)
				{
					task.Status = AgentTaskStatus.Cancelled;
					completionSource.SetCanceled(cancellationToken);
				}
				catch (Exception ex)
				{
					task.Status = AgentTaskStatus.Failed;
					task.Exception = ex;
					completionSource.SetException(ex);
				}
				finally
				{
					task.Completed = true;
					_dispatcher.OnEndTask(task);
				}
			}, CancellationToken.None);

			return task;
		}

		private async Task ExecuteAsync(AgentTask task, LLModel model,
			ImmutableDictionary<string, IToolDescriptor> tools, List<IMessage> nativeMessages)
		{
			var cancellationToken = task.CancellationTokenSource.Token;
			var semaphore = new SemaphoreSlim(task.LaunchParameters.MaxParallelToolCalls, task.LaunchParameters.MaxParallelToolCalls);

			var executionTimer = new Stopwatch();
			executionTimer.Start();

			int totalInputTokens = 0, totalOutputTokens = 0;
			int totalCacheHitTokens = 0, totalCacheMissTokens = 0;
			TimeSpan totalTtft = TimeSpan.Zero, totalInferenceTime = TimeSpan.Zero;

			while (true)
			{
				Stopwatch messageExecutionTimer = new(), inferenceTimer = new();
				messageExecutionTimer.Start();
				inferenceTimer.Start();

				try
				{
					cancellationToken.ThrowIfCancellationRequested();

					TimeSpan? ttft = null;
					TimeSpan inferenceTime = TimeSpan.Zero;

					var response = await model.ChatStreamingAsync(nativeMessages, cancellationToken: cancellationToken);
					IAssistantMessage responseMessage = response.Message;
					nativeMessages.Add(responseMessage);

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
							return await ExecuteToolAsync(toolCall, agentMessage, task, tools, cancellationToken);
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
							ttft ??= executionTimer.Elapsed;

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
					totalTtft += ttft.Value;
					totalInferenceTime += inferenceTime;

					int inputTokens = 0, outputTokens = 0;
					int cacheHitTokens = 0, cacheMissTokens = 0;

					if (response.UsageMetadata is IUsageMetadata usageMetadata)
					{
						inputTokens = usageMetadata.InputTokens;
						outputTokens = usageMetadata.OutputTokens;

						if (usageMetadata is IUsageCacheMetadata cacheMetadata)
						{
							cacheHitTokens = cacheMetadata.InputCacheHitTokens;
							cacheMissTokens = cacheMetadata.InputCacheMissTokens;

							if (cacheHitTokens < 0) cacheHitTokens = 0;
							if (cacheMissTokens < 0) cacheMissTokens = 0;
						}

						if (inputTokens < 0) inputTokens = 0;
						if (outputTokens < 0) outputTokens = 0;
					}

					totalInputTokens += inputTokens;
					totalOutputTokens += outputTokens;
					totalCacheHitTokens += cacheHitTokens;
					totalCacheMissTokens += cacheMissTokens;

					agentMessage.UsageStatistics = new AgentUsageStatistics
					{
						InputTokens = inputTokens,
						OutputTokens = outputTokens,
						InputCacheHitTokens = cacheHitTokens,
						InputCacheMissTokens = cacheMissTokens,
						TimeToFirstToken = ttft.Value,
						InferenceTime = inferenceTime,
						ExecutionTime = messageExecutionTimer.Elapsed
					};

					task.UsageStatistics = new AgentUsageStatistics
					{
						InputTokens = totalInputTokens,
						OutputTokens = totalOutputTokens,
						InputCacheHitTokens = totalCacheHitTokens,
						InputCacheMissTokens = totalCacheMissTokens,
						TimeToFirstToken = totalTtft,
						InferenceTime = totalInferenceTime,
						ExecutionTime = executionTimer.Elapsed
					};

					_usageStatsCollector.RecordUsage(model.Descriptor.FullName,
						inputTokens, outputTokens,
						cacheHitTokens, cacheMissTokens,
						messageExecutionTimer.ElapsedMilliseconds,
						success: true);

					nativeMessages.AddRange(await Task.WhenAll(toolCallTasks));

					if (task.LaunchParameters.Behaviour
						is AgentTaskExecutionBehaviour.ExecuteOnce or AgentTaskExecutionBehaviour.OnlyResponse)
						break;

					if (agentMessage.ToolCalls.Count == 0)
					{
						if (task.LaunchParameters.FeedbackFunc is not null)
						{
							var feedbackMessage = await task.LaunchParameters.FeedbackFunc.Invoke(task, cancellationToken);
							if (feedbackMessage != null)
							{
								task.Messages.Add(feedbackMessage);
								nativeMessages.Add(new UserMessage(Senders.User, feedbackMessage.Content ?? string.Empty,
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
					_usageStatsCollector.RecordUsage(model.Descriptor.FullName,
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

			executionTimer.Stop();
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
				Descriptor = descriptor,
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

		private static IEnumerable<IMessage> ConvertMessageFromAgent(AgentChatMessage agentMessage, ref string? additionalSysPrompt)
		{
			switch (agentMessage)
			{
				case AgentSystemMessage systemMessage:
					if (additionalSysPrompt != null)
					{
						var addPrompt = additionalSysPrompt;
						additionalSysPrompt = null;
						if (string.IsNullOrEmpty(systemMessage.Content))
						{
							return [new SystemMessage(addPrompt)];
						}
						else
						{
							return [new SystemMessage($"""
								{systemMessage.Content}

								{addPrompt}
								""")];
						}
					}

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
