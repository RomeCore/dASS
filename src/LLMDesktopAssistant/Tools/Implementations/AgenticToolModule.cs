using System.ComponentModel;
using System.Globalization;
using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils.Attachments;
using Material.Icons;
using RCLargeLanguageModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule]
	public class AgenticToolModule : ToolModule
	{
		private const int maxImagesPerCall = 4;
		private const long maxImageBytes = 5 * 1024 * 1024;
		private const long maxTotalImageBytes = 15 * 1024 * 1024;
		private const int maxListedTasks = 100;
		private const int maxListSnippetLength = 200;
		private const int maxWaitSeconds = 300;
		private const int cancelPollIntervalMilliseconds = 100;
		private const int cancelPollAttempts = 30;

		// A background call exists to be returned to later, so a completed task outlives the 5 minute default.
		private static readonly TimeSpan agentTaskExpiry = TimeSpan.FromHours(2);

		// The launch sources this module owns: its two calls plus the sub-agent tasks they spawn.
		private static readonly string[] trackedSourceIds =
			["chat-agent-call", "agent-callsub", "task-agent-callsub"];

		private readonly Chat _chat;
		private readonly IChatSettingsService _chatSettings;
		private readonly ITemplateLibraryAccessor _templates;
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IAgentManagementService _agentManager;
		private readonly IAgentTaskExecutor _agentTaskExecutor;
		private readonly IModelManager _modelManager;
		private readonly IAddonSetCollector<ToolInfo> _toolsetBuildingService;
		private readonly IAddonSetCollector<SkillInfo> _skillsetBuildingService;
		private readonly IAddonSetCollector<SubAgentInfo> _subAgentsetCollector;
		private readonly ISubAgentTaskParamsResolver _subAgentParamsResolver;

		public AgenticToolModule(Chat chat, IChatSettingsService chatSettings, ITemplateLibraryAccessor templates,
			IWorkingDirectoryAccessService fileAccess,
			IAgentManagementService agentManager, IAgentTaskExecutor agentTaskExecutor, IModelManager modelManager,
			IAddonSetCollector<ToolInfo> toolsetBuildingService, IAddonSetCollector<SkillInfo> skillsetBuildingService,
			IAddonSetCollector<SubAgentInfo> subAgentsetCollector, ISubAgentTaskParamsResolver subAgentParamsResolver)
		{
			_chat = chat;
			_chatSettings = chatSettings;
			_templates = templates;
			_fileAccess = fileAccess;
			_agentManager = agentManager;
			_agentTaskExecutor = agentTaskExecutor;
			_modelManager = modelManager;
			_toolsetBuildingService = toolsetBuildingService;
			_skillsetBuildingService = skillsetBuildingService;
			_subAgentsetCollector = subAgentsetCollector;
			_subAgentParamsResolver = subAgentParamsResolver;

			AddTool(new ToolInitializationInfo
			{
				Executor = CallAgent,
				PreviewExecutor = CallAgentPreview,
				Name = "agent-call",
				Description = "Calls another AI agent with provided system message and user message with set of " +
					"allowed tools. Images from files can be attached to the user message.",
				NameKey = Locale.GetKey("tool.name.agent-call"),
				DescriptionKey = Locale.GetKey("tool.description.agent-call"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask |
					ToolBehaviour.FileRead
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CallSubAgent,
				PreviewExecutor = CallSubAgentPreview,
				Name = "agent-callsub",
				Description = "Calls another predefined AI agent with provided input. Images from files can be " +
					"attached to the input.",
				NameKey = Locale.GetKey("tool.name.agent-callsub"),
				DescriptionKey = Locale.GetKey("tool.description.agent-callsub"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask |
					ToolBehaviour.FileRead
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ListTasks,
				Name = "agent-task_list",
				Description = "Lists the agent tasks this chat launched in the background with agent-call or " +
					"agent-callsub, with their status, elapsed time and last message.",
				NameKey = Locale.GetKey("tool.name.agent-task_list"),
				DescriptionKey = Locale.GetKey("tool.description.agent-task_list"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.None
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = WaitTask,
				Name = "agent-task_wait",
				Description = "Waits for an agent task to finish and returns its last message and images. " +
					"When the timeout is reached it reports the current status, so it can be called again.",
				NameKey = Locale.GetKey("tool.name.agent-task_wait"),
				DescriptionKey = Locale.GetKey("tool.description.agent-task_wait"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.LongRunningTask
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ContinueTask,
				PreviewExecutor = ContinueTaskPreview,
				Name = "agent-task_continue",
				Description = "Sends a follow-up message to a running or a completed agent task, with optional " +
					"images, and optionally waits for the result.",
				NameKey = Locale.GetKey("tool.name.agent-task_continue"),
				DescriptionKey = Locale.GetKey("tool.description.agent-task_continue"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.FileRead
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CancelTask,
				Name = "agent-task_cancel",
				Description = "Cancels an agent task together with all of its sub-tasks.",
				NameKey = Locale.GetKey("tool.name.agent-task_cancel"),
				DescriptionKey = Locale.GetKey("tool.description.agent-task_cancel"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.None
			});
		}

		private PreviewToolExecutionResult CallAgentPreview(IToolExecutionContext ctx, string[]? images) =>
			ImageAccessPreview(ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask | ToolBehaviour.FileRead,
				images);

		private PreviewToolExecutionResult CallSubAgentPreview(IToolExecutionContext ctx, string[]? images) =>
			ImageAccessPreview(ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask | ToolBehaviour.FileRead,
				images);

		private PreviewToolExecutionResult ContinueTaskPreview(IToolExecutionContext ctx, string[]? images,
			bool wait = false) =>
			ImageAccessPreview(ToolBehaviour.AgentExecution | ToolBehaviour.FileRead |
				(wait ? ToolBehaviour.LongRunningTask : ToolBehaviour.None), images);

		/// <summary>
		/// Widens the tool behaviour by what the image arguments really do: reading outside the working
		/// directory is a separate consent and must be priced before the call runs.
		/// </summary>
		private PreviewToolExecutionResult ImageAccessPreview(ToolBehaviour behaviour, string[]? images)
		{
			foreach (var image in (images ?? []).Where(path => !string.IsNullOrWhiteSpace(path)))
			{
				_fileAccess.CheckedAccessPath(image, DirectoryAccessMode.Read, out var isAccessed);
				behaviour |= ToolBehaviour.FileRead;

				if (!isAccessed)
					behaviour |= ToolBehaviour.AccessOutsideWorkdir;
			}

			return new PreviewToolExecutionResult { ExpectedBehaviour = behaviour };
		}

		/// <summary>
		/// The tasks this chat launched through this module, in chat order. A task that has expired is no
		/// longer in the chat collection and therefore no longer resolvable.
		/// </summary>
		private static IEnumerable<AgentTask> GetTrackedTasks(Chat? chat) =>
			chat is null
				? []
				: chat.AgentTasks.Where(task =>
					task.LaunchParameters.Source is AgentTaskLaunchSource.Tool &&
					!task.LaunchParameters.Hidden &&
					trackedSourceIds.Contains(task.LaunchParameters.SourceId));

		private static AgentTask? ResolveTask(IToolExecutionContext ctx, string taskId) =>
			Guid.TryParse(taskId, out var id)
				? GetTrackedTasks(ctx.Chat).FirstOrDefault(task => task.Id == id)
				: null;

		private static string DescribeTitle(AgentTask task) => task.LaunchParameters.TaskName ?? "(untitled)";

		private static string DescribeSource(AgentTask task) =>
			task.LaunchParameters.SourceId == "chat-agent-call" ? "call" : "callsub";

		private static string NotFoundMessage(IToolExecutionContext ctx, string taskId)
		{
			var known = GetTrackedTasks(ctx.Chat).Select(task => task.Id.ToString()).Take(10).ToArray();
			var message = $"Task not found: {taskId}. It may have expired (a completed task is kept for " +
				$"{agentTaskExpiry.TotalHours:0} hours) or belong to another chat.";

			return known.Length == 0
				? message
				: message + " Known task ids in this chat: " + string.Join(", ", known) + ".";
		}

		private async Task<ToolExecutionResult> CallAgent(
			[Description("The title of the agent call to be visible in UI")] string? callTitle,
			[Description("The system prompt to use in the agent's context")] string systemPrompt,
			[Description("The user message to send to the agent")] string userMessage,
			[Description("A list of tool names that can be used by agent.")] string[] allowedTools,
			[Description("A list of skill names that can be used by agent.")] string[] allowedSkills,
			[Description("A list of sub-agent names that can be used by agent.")] string[] allowedSubAgents,
			[Description("""
				A list of memory block names to make accessible to the agent via memory tools.
				Memory must be enabled for the chat and the calling agent.
				""")] string[] allowedMemoryBlocks,
			IToolExecutionContext ctx,
			[Description("""
				Paths to image files to attach to the agent's user message. Relative paths are resolved against
				the working directory; at most 4 images, 5 MB each and 15 MB in total.
				""")] string[]? images = null,
			[Description("""
				If true - waits for end of execution and returns the contents of last message.
				If false - returns agent task ID immediately, the agent will continue to run in the background.
				""")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var attachments = ResolveImages(images, out var imageError);
			if (imageError is not null)
				return Fail(ctx, imageError);

			var modelName = _chatSettings.Settings.Models.GetEffectiveSelection().AgenticToolsModel;
			if (string.IsNullOrEmpty(modelName))
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult("No agentic model selected. Say user to select an agentic model first.");
				return ToolExecutionResult.Failed;
			}

			LLModel llm;
			try
			{
				llm = _modelManager.GetModel(modelName);
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult($"Agentic model '{modelName}' is not available: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			// Returns null if tool initiator is user
			var agentDescriptor = ctx is ChatToolExecutionContext chatContext
				? _agentManager.TryGetSenderAgentDescriptor(chatContext.Message)
				: null;

			var errorSb = new StringBuilder();

			var tools = ImmutableList.CreateBuilder<IToolDescriptor>();
			if (allowedTools.Length > 0)
			{
				var toolMap = agentDescriptor is not null ?
					_toolsetBuildingService.GetAddonsForAgent(agentDescriptor) :
					_toolsetBuildingService.GetAvailableAddons();

				int notFound = 0;
				foreach (var allowedTool in allowedTools.Distinct())
				{
					if (toolMap.GetByName(allowedTool) is { } toolInfo)
					{
						tools.Add(toolInfo);
					}
					else
					{
						notFound++;
						errorSb.AppendLine("Tool was not found: " + allowedTool);
					}
				}

				if (notFound > 0)
					errorSb.Append("Valid tool names: " + string.Join(", ", toolMap.Select(t => t.Name)));
			}

			var skills = ImmutableList.CreateBuilder<AgentSkill>();
			if (allowedSkills.Length > 0)
			{
				var skillMap = agentDescriptor is not null ?
					_skillsetBuildingService.GetAddonsForAgent(agentDescriptor) :
					_skillsetBuildingService.GetAvailableAddons();

				int notFound = 0;
				foreach (var allowedSkill in allowedSkills.Distinct())
				{
					if (skillMap.GetByName(allowedSkill) is { } skillInfo)
					{
						skills.Add(new ChatAgentSkill(skillInfo));
					}
					else
					{
						notFound++;
						errorSb.AppendLine("Skill was not found: " + allowedSkill);
					}
				}

				if (notFound > 0)
					errorSb.Append("Valid skill names: " + string.Join(", ", skillMap.Select(t => t.Name)));
			}

			var subAgents = ImmutableList.CreateBuilder<TaskSubAgentDescriptor>();
			if (allowedSubAgents.Length > 0)
			{
				var subAgentMap = agentDescriptor is not null ?
					_subAgentsetCollector.GetAddonsForAgent(agentDescriptor) :
					_subAgentsetCollector.GetAvailableAddons();

				int notFound = 0;
				foreach (var allowedSubAgent in allowedSubAgents.Distinct())
				{
					if (subAgentMap.GetByName(allowedSubAgent) is { } subAgentInfo)
					{
						subAgents.Add(new TaskSubAgentDescriptor
						{
							Name = subAgentInfo.Name,
							Description = subAgentInfo.Description
						});
					}
					else
					{
						notFound++;
						errorSb.AppendLine("Sub-agent was not found: " + allowedSubAgent);
					}
				}

				if (notFound > 0)
					errorSb.AppendLine("Valid sub-agent names: " + string.Join(", ", subAgentMap.Select(t => t.Name)));
			}

			var memoryBlocks = ImmutableList.CreateBuilder<TaskMemoryBlock>();
			if (allowedMemoryBlocks.Length > 0)
			{
				var available = TaskMemoryBlock.ResolveBlocks(_chat, agentDescriptor);
				var availableMap = available.ToDictionary(b => b.Block.Name);

				int notFound = 0;
				foreach (var blockName in allowedMemoryBlocks.Distinct())
				{
					if (availableMap.TryGetValue(blockName, out var block))
					{
						memoryBlocks.Add(block);
					}
					else
					{
						notFound++;
						errorSb.AppendLine("Memory block was not found: " + blockName);
					}
				}

				if (notFound > 0)
					errorSb.Append("Valid memory block names: " + string.Join(", ", availableMap.Keys));
			}

			if (errorSb.Length > 0)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult(errorSb.ToString());
				return ToolExecutionResult.Failed;
			}

			var policy = agentDescriptor?.Tools.GetEffectivePolicy(_chatSettings.Settings) ?? default;
			ToolBehaviour autoApproveBehaviours = policy.AutoApproveBehaviours,
				disallowedBehaviours = policy.DisallowedBehaviours;

			ctx.Call.SetStatusIcon(MaterialIconKind.Robot);

			try
			{
				var ct = wait ? cancellationToken : CancellationToken.None;

				var agentTask = _agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = callTitle,
					Source = AgentTaskLaunchSource.Tool,
					SourceId = "chat-agent-call",
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					Model = llm,
					Tools = tools.ToImmutableList(),
					Skills = skills.ToImmutableList(),
					SubAgents = subAgents.ToImmutableList(),
					MemoryBlocks = memoryBlocks.ToImmutableList(),
					InitialMessages = [
						new AgentSystemMessage { Content = systemPrompt },
						new AgentUserMessage { Content = userMessage, Attachments = [.. attachments] }
					],
					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours,
					CompletionExpiryTime = agentTaskExpiry
				}, ct);

				if (wait)
				{
					await agentTask;

					ctx.Call.UseMarkdown = true;
					ctx.Call.SetResult(string.IsNullOrWhiteSpace(agentTask.LastGeneratedContent) ?
						"Agent did not generate any content." : agentTask.LastGeneratedContent);
					AttachResultImages(ctx, agentTask);
					return ToolExecutionResult.Succeeded;
				}
				else
				{
					ctx.Call.SetResult($"Agent launched with task ID {agentTask.Id}.");
					return ToolExecutionResult.Succeeded;
				}
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult("An error occurred while calling the agent: " + ex.Message);
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> CallSubAgent(
			[Description("The name of predefined sub-agent")] string agentName,
			[Description("The user message to send to the agent")] string input,
			IToolExecutionContext ctx,
			[Description("""
				Paths to image files to attach to the input message. Relative paths are resolved against the
				working directory; at most 4 images, 5 MB each and 15 MB in total.
				""")] string[]? images = null,
			[Description("""
				If true - waits for end of execution and returns the contents of last message.
				If false - returns agent task ID immediately, the agent will continue to run in the background.
				""")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var attachments = ResolveImages(images, out var imageError);
			if (imageError is not null)
				return Fail(ctx, imageError);

			var agentDescriptor = ctx is ChatToolExecutionContext chatContext
				? _agentManager.TryGetSenderAgentDescriptor(chatContext.Message)
				: null;

			var subAgent = agentDescriptor is not null ?
				_subAgentsetCollector.GetAddonsForAgent(agentDescriptor).GetByName(agentName) :
				_subAgentsetCollector.GetAvailableAddons().GetByName(agentName);
			if (subAgent is null)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult($"Sub-agent '{agentName}' not found.");
				return ToolExecutionResult.Failed;
			}

			var policy = agentDescriptor?.Tools.GetEffectivePolicy(_chatSettings.Settings) ?? default;
			ToolBehaviour autoApproveBehaviours = policy.AutoApproveBehaviours,
				disallowedBehaviours = policy.DisallowedBehaviours;

			AgentTaskLaunchParameters parameters;
			try
			{
				parameters = _subAgentParamsResolver.Resolve(new AgentTaskLaunchParameters
				{
					TaskName = subAgent.Name,
					Source = AgentTaskLaunchSource.Tool,
					// The resolver replaces this with the source of the task it actually builds.
					SourceId = "agent-callsub",
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					InitialMessages = [],
					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours,
					CompletionExpiryTime = agentTaskExpiry
				}, new TaskSubAgentDescriptor
				{
					Name = subAgent.Name,
					Description = subAgent.Description
				}, [new AgentUserMessage { Content = input, Attachments = [.. attachments] }], out var errors);

				if (errors.Count > 0)
				{
					ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
					ctx.Call.SetResult(string.Join(Environment.NewLine, errors));
					return ToolExecutionResult.Failed;
				}
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult("An error occurred while resolving the sub-agent: " + ex.Message);
				return ToolExecutionResult.Failed;
			}

			ctx.Call.SetStatusIcon(MaterialIconKind.Robot);

			try
			{
				var ct = wait ? cancellationToken : CancellationToken.None;

				var agentTask = _agentTaskExecutor.Execute(parameters, ct);

				if (wait)
				{
					await agentTask;

					ctx.Call.UseMarkdown = true;
					ctx.Call.SetResult(string.IsNullOrWhiteSpace(agentTask.LastGeneratedContent) ?
						"Sub-agent did not generate any content." : agentTask.LastGeneratedContent);
					AttachResultImages(ctx, agentTask);
					return ToolExecutionResult.Succeeded;
				}

				ctx.Call.SetResult($"Sub-agent launched with task ID {agentTask.Id}.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
				ctx.Call.SetResult("An error occurred while calling the sub-agent: " + ex.Message);
				return ToolExecutionResult.Failed;
			}
		}

		private Task<ToolExecutionResult> ListTasks(
			IToolExecutionContext ctx,
			[Description("Whether tasks that already completed are included")] bool includeCompleted = true,
			[Description("Whether sub-tasks spawned by other tasks are included")] bool includeSubTasks = true,
			[Description("How many tasks to return at most")] int limit = 20)
		{
			var now = DateTime.Now;
			var tasks = GetTrackedTasks(ctx.Chat)
				.Where(task => includeCompleted || !task.Completed)
				.Where(task => includeSubTasks || task.Parent is null)
				.OrderBy(task => task.Completed)
				.ThenByDescending(task => task.StartedAt)
				.Take(Math.Clamp(limit, 1, maxListedTasks))
				.ToArray();

			ctx.Call.UseMarkdown = true;

			if (tasks.Length == 0)
			{
				ctx.Call.SetResult("No agent tasks in this chat.");
				return Task.FromResult(ToolExecutionResult.Succeeded);
			}

			var sb = new StringBuilder();
			sb.AppendLine("| id | title | status | tool | elapsed | parent | last |");
			sb.AppendLine("|---|---|---|---|---|---|---|");

			foreach (var task in tasks)
			{
				sb.AppendLine($"| {task.Id} | {TableCell(DescribeTitle(task))} | {task.Status} | " +
					$"{DescribeSource(task)} | {FormatElapsed(task, now)} | {task.Parent?.Id.ToString() ?? "-"} | " +
					$"{TableSnippet(task.LastGeneratedContent)} |");
			}

			ctx.Call.SetResult(sb.ToString().TrimEnd());
			return Task.FromResult(ToolExecutionResult.Succeeded);
		}

		/// <summary>
		/// How long the current launch has been running, or how long the finished one took.
		/// </summary>
		private static string FormatElapsed(AgentTask task, DateTime now)
		{
			var end = task.Completed ? task.CompletedAt ?? now : now;
			var elapsed = end - task.StartedAt;

			if (elapsed < TimeSpan.Zero)
				elapsed = TimeSpan.Zero;

			return elapsed.TotalHours >= 1
				? $"{(int)elapsed.TotalHours}h{elapsed.Minutes:00}m"
				: $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:00}s";
		}

		private static string TableCell(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "-";

			return value.ReplaceLineEndings(" ").Replace("|", "\\|").Trim();
		}

		private static string TableSnippet(string? content)
		{
			if (string.IsNullOrWhiteSpace(content))
				return "-";

			var snippet = TableCell(content);
			return snippet.Length <= maxListSnippetLength
				? snippet
				: snippet[..maxListSnippetLength] + "...";
		}

		private async Task<ToolExecutionResult> WaitTask(
			[Description("The id of the agent task to wait for")] string taskId,
			IToolExecutionContext ctx,
			[Description("How many seconds to wait at most")] int timeoutSeconds = 60,
			CancellationToken cancellationToken = default)
		{
			var task = ResolveTask(ctx, taskId);
			if (task is null)
				return Fail(ctx, NotFoundMessage(ctx, taskId));

			var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, maxWaitSeconds));
			ctx.Call.SetStatusIcon(MaterialIconKind.TimerSand);
			ctx.Call.SetStatusTitle($"**{TableCell(DescribeTitle(task))}**");

			if (!task.Completed)
			{
				var delayTask = Task.Delay(timeout, cancellationToken);
				var finished = await Task.WhenAny(task.Completion, delayTask) == task.Completion;
				cancellationToken.ThrowIfCancellationRequested();

				if (!finished)
				{
					ctx.Call.SetResult($"Task {task.Id} is still {task.Status} after " +
						$"{timeout.TotalSeconds:0} seconds. Call agent-task_wait again to keep waiting.");
					return ToolExecutionResult.Succeeded;
				}
			}

			await ObserveCompletionAsync(task);

			ctx.Call.SetStatusIcon(task.Status switch
			{
				AgentTaskStatus.Success => MaterialIconKind.Check,
				AgentTaskStatus.Cancelled => MaterialIconKind.Cancel,
				_ => MaterialIconKind.AlertCircle
			});
			ctx.Call.SetStatusTitle($"**{TableCell(DescribeTitle(task))}**");
			ctx.Call.UseMarkdown = true;
			ctx.Call.SetResult(DescribeOutcome(task));
			AttachResultImages(ctx, task);

			// A failed task is a fact about the task, not a failure of the wait itself.
			return ToolExecutionResult.Succeeded;
		}

		private async Task<ToolExecutionResult> ContinueTask(
			[Description("The id of the agent task to continue")] string taskId,
			[Description("The message to deliver to the task")] string message,
			IToolExecutionContext ctx,
			[Description("""
				Paths to image files to attach to the message. Relative paths are resolved against the working
				directory; at most 4 images, 5 MB each and 15 MB in total.
				""")] string[]? images = null,
			[Description("""
				If true - waits for the task to finish and returns its last message.
				If false - returns immediately, the task continues in the background.
				""")] bool wait = false,
			CancellationToken cancellationToken = default)
		{
			var task = ResolveTask(ctx, taskId);
			if (task is null)
				return Fail(ctx, NotFoundMessage(ctx, taskId));

			var attachments = ResolveImages(images, out var imageError);
			if (imageError is not null)
				return Fail(ctx, imageError);

			if (string.IsNullOrWhiteSpace(message) && attachments.IsEmpty)
				return Fail(ctx, "A follow-up must carry a message or at least one image.");

			var wasCompleted = task.Completed;
			task.Continue([new AgentUserMessage { Content = message, Attachments = [.. attachments] }]);

			ctx.Call.SetStatusIcon(MaterialIconKind.Robot);

			if (!wait)
			{
				ctx.Call.SetResult(wasCompleted
					? $"Task {task.Id} restarted ({task.Status}). Use agent-task_wait to collect the result."
					: $"Follow-up accepted by task {task.Id} ({task.Status}). It is delivered after the task's " +
						"current step; use agent-task_wait to collect the result.");
				return ToolExecutionResult.Succeeded;
			}

			await ObserveCompletionAsync(task);

			ctx.Call.UseMarkdown = true;
			ctx.Call.SetResult(DescribeOutcome(task));
			AttachResultImages(ctx, task);
			return ToolExecutionResult.Succeeded;
		}

		private async Task<ToolExecutionResult> CancelTask(
			IToolExecutionContext ctx,
			[Description("The id of the agent task to cancel")] [MainArgument] string taskId,
			[Description("Whether to cancel all sub-tasks too")] bool cascade = true,
			CancellationToken cancellationToken = default)
		{
			var task = ResolveTask(ctx, taskId);
			if (task is null)
				return Fail(ctx, NotFoundMessage(ctx, taskId));

			if (task.Completed)
			{
				ctx.Call.SetResult($"Task {task.Id} is already completed with status {task.Status}, " +
					"nothing to cancel.");
				return ToolExecutionResult.Succeeded;
			}

			ctx.Call.SetStatusIcon(MaterialIconKind.Cancel);

			if (cascade)
			{
				static void CancelRecursively(AgentTask task)
				{
					foreach (var subTask in task.SubTasks)
						CancelRecursively(subTask);

					task.CancellationTokenSource.Cancel();
				}
				CancelRecursively(task);
			}
			else
				task.CancellationTokenSource.Cancel();

			// The status is written by the executor after the cancellation unwinds, so a short poll is
			// what turns "cancellation requested" into an honest answer.
			for (int attempt = 0; attempt < cancelPollAttempts && !task.Completed; attempt++)
				await Task.Delay(cancelPollIntervalMilliseconds, cancellationToken);

			// A cancelled task reports Cancelled only after the cancellation unwinds; a task that finished
			// first keeps its own status, and saying "cancelled" then would be a lie.
			ctx.Call.SetResult(!task.Completed
				? $"Cancellation requested for task {task.Id}, it is still {task.Status}."
				: task.Status is AgentTaskStatus.Cancelled
					? $"Task {task.Id} cancelled."
					: $"Cancellation was requested for task {task.Id}, but it finished with status " +
						$"{task.Status} before the request took effect.");

			return ToolExecutionResult.Succeeded;
		}

		/// <summary>
		/// Awaits the task only as a completion signal: a cancelled or failed task reports its own outcome
		/// through its status, so the exception must not escape.
		/// </summary>
		private static async Task ObserveCompletionAsync(AgentTask task)
		{
			try
			{
				await task.Completion;
			}
			catch (Exception)
			{
				// The status describes the outcome; the await is only the wake-up.
			}
		}

		private static string DescribeOutcome(AgentTask task)
		{
			var sb = new StringBuilder();
			sb.Append($"Task {task.Id} ({DescribeTitle(task)}) finished with status {task.Status}.");

			if (!string.IsNullOrWhiteSpace(task.LastGeneratedContent))
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.Append(task.LastGeneratedContent);
			}
			else if (task.Exception is { } exception)
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.Append("Error: " + exception.Message);
			}

			return sb.ToString();
		}

		/// <summary>
		/// Hands the images of the task's last message to the calling chat. Only images travel: a chip has
		/// no form for anything else, and dropping them silently would hide real output.
		/// </summary>
		/// <remarks>
		/// Only the message's own attachments are taken, never the tool call results. A task stops on a
		/// message without tool calls, so those results sit on earlier messages, and feeding them back
		/// would hand the caller the very image it has just described with attach-describe_image. The path
		/// this exists for is a model that returns an image in its own reply, like a diffusion model.
		/// </remarks>
		private static void AttachResultImages(IToolExecutionContext ctx, AgentTask task)
		{
			if (task.LastGeneratedMessage is not { } message)
				return;

			foreach (var attachment in message.Attachments.OfType<AgentNativeAttachment>())
			{
				if (attachment.Type is not AttachmentKind.Image)
					continue;

				ctx.Call.AddAttachment(new NativeAttachmentMessagePart
				{
					NativeAttachment = new SerializableImageAttachment(attachment.Url),
					ChipTitle = Locale.GetConstKey(DescribeTitle(task)),
					ChipIcon = MaterialIconKind.Image,
					IsVisible = true
				});
			}
		}

		/// <summary>
		/// Turns image file paths into native attachments, refusing the whole set when one of them breaks
		/// the limits, so a half-attached call never reaches the model.
		/// </summary>
		private ImmutableList<AgentAttachment> ResolveImages(string[]? images, out string? error)
		{
			error = null;

			if (images is null || images.Length == 0)
				return [];

			var paths = images.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray();
			if (paths.Length > maxImagesPerCall)
			{
				error = $"Too many images: {paths.Length}. At most {maxImagesPerCall} images per call.";
				return [];
			}

			var attachments = ImmutableList.CreateBuilder<AgentAttachment>();
			long totalBytes = 0;

			foreach (var image in paths)
			{
				var path = _fileAccess.CheckedAccessPath(image, DirectoryAccessMode.Read, out _);
				if (!File.Exists(path))
				{
					error = $"Image not found: {image}";
					return [];
				}

				var length = new FileInfo(path).Length;
				if (length > maxImageBytes)
				{
					error = $"Image is too large: {image} ({Megabytes(length)} MB). " +
						$"At most {Megabytes(maxImageBytes)} MB per image.";
					return [];
				}

				totalBytes += length;
				if (totalBytes > maxTotalImageBytes)
				{
					error = $"Images are too large together: {Megabytes(totalBytes)} MB. " +
						$"At most {Megabytes(maxTotalImageBytes)} MB per call.";
					return [];
				}

				IImageFormat format;
				try
				{
					format = Image.DetectFormat(path);
				}
				catch (Exception)
				{
					error = $"Not a supported image: {image}";
					return [];
				}

				attachments.Add(AgentNativeAttachment.FromBase64(AttachmentKind.Image,
					format.Name.ToLowerInvariant(), Convert.ToBase64String(File.ReadAllBytes(path))));
			}

			return attachments.ToImmutable();
		}

		private static string Megabytes(long bytes) =>
			(bytes / (1024.0 * 1024.0)).ToString("0.##", CultureInfo.InvariantCulture);

		private static ToolExecutionResult Fail(IToolExecutionContext ctx, string message)
		{
			ctx.Call.SetStatusIcon(MaterialIconKind.RobotDead);
			ctx.Call.SetResult(message);
			return ToolExecutionResult.Failed;
		}
	}
}
