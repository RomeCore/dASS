using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Utils.Attachments;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using RCLargeLanguageModels;
using SixLabors.ImageSharp;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule]
	public class AgenticToolModule : ToolModule
	{
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
				Name = "agent-call",
				Description = "Calls another AI agent with provided system message and user message with set of allowed tools.",
				NameKey = Locale.GetKey("tool.name.agent-call"),
				DescriptionKey = Locale.GetKey("tool.description.agent-call"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CallSubAgent,
				Name = "agent-callsub",
				Description = "Calls another predefined AI agent with provided input.",
				NameKey = Locale.GetKey("tool.name.agent-callsub"),
				DescriptionKey = Locale.GetKey("tool.description.agent-callsub"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DescribeImage,
				StreamingAnalyzer = DescribeImageStreaming,
				PreviewExecutor = DescribeImagePreview,
				Name = "agent-describe_image",
				Description = "Describes an image using another LLM agent.",
				NameKey = Locale.GetKey("tool.name.agent-describe_image"),
				DescriptionKey = Locale.GetKey("tool.description.agent-describe_image"),
				CategoryKey = Locale.GetKey("tool.category.agents"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask |
					ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});
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
				If true - waits for end of execution and returns the contents of last message.
				If false - returns agent task ID immediately, the agent will continue to run in the background.
				""")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
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
					_toolsetBuildingService.GetAddonsForAgent(agentDescriptor).ToDictionary(t => t.Name) :
					_toolsetBuildingService.GetAvailableAddons().ToDictionary(t => t.Name);

				int notFound = 0;
				foreach (var allowedTool in allowedTools.Distinct())
				{
					if (toolMap.TryGetValue(allowedTool, out var toolInfo))
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
					errorSb.Append("Valid tool names: " + string.Join(", ", toolMap.Keys));
			}

			var skills = ImmutableList.CreateBuilder<AgentSkill>();
			if (allowedSkills.Length > 0)
			{
				var skillMap = agentDescriptor is not null ?
					_skillsetBuildingService.GetAddonsForAgent(agentDescriptor).ToDictionary(t => t.Name) :
					_skillsetBuildingService.GetAvailableAddons().ToDictionary(t => t.Name);

				int notFound = 0;
				foreach (var allowedSkill in allowedSkills.Distinct())
				{
					if (skillMap.TryGetValue(allowedSkill, out var skillInfo))
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
					errorSb.Append("Valid skill names: " + string.Join(", ", skillMap.Keys));
			}

			var subAgents = ImmutableList.CreateBuilder<TaskSubAgentDescriptor>();
			if (allowedSubAgents.Length > 0)
			{
				var subAgentMap = agentDescriptor is not null ?
					_subAgentsetCollector.GetAddonsForAgent(agentDescriptor).ToDictionary(t => t.Name) :
					_subAgentsetCollector.GetAvailableAddons().ToDictionary(t => t.Name);

				int notFound = 0;
				foreach (var allowedSubAgent in allowedSubAgents.Distinct())
				{
					if (subAgentMap.TryGetValue(allowedSubAgent, out var subAgentInfo))
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
					errorSb.AppendLine("Valid sub-agent names: " + string.Join(", ", subAgentMap.Keys));
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
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					Model = llm,
					Tools = tools.ToImmutableList(),
					Skills = skills.ToImmutableList(),
					SubAgents = subAgents.ToImmutableList(),
					MemoryBlocks = memoryBlocks.ToImmutableList(),
					InitialMessages = [
						new AgentSystemMessage { Content = systemPrompt },
						new AgentUserMessage { Content = userMessage }
					],
					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours
				}, ct);

				if (wait)
				{
					await agentTask;

					ctx.Call.SetResult(string.IsNullOrWhiteSpace(agentTask.LastGeneratedContent) ?
							"Agent did not generate any content." : agentTask.LastGeneratedContent);
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
				If true - waits for end of execution and returns the contents of last message.
				If false - returns agent task ID immediately, the agent will continue to run in the background.
				""")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var agentDescriptor = ctx is ChatToolExecutionContext chatContext
				? _agentManager.TryGetSenderAgentDescriptor(chatContext.Message)
				: null;

			var subAgent = agentDescriptor is not null ?
				_subAgentsetCollector.GetAddonsForAgent(agentDescriptor).FirstOrDefault(a => a.Name == agentName) :
				_subAgentsetCollector.GetAvailableAddons().FirstOrDefault(a => a.Name == agentName);
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
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					InitialMessages = [],
					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours
				}, new TaskSubAgentDescriptor
				{
					Name = subAgent.Name,
					Description = subAgent.Description
				}, [new AgentUserMessage { Content = input }], out var errors);

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

					ctx.Call.SetResult(string.IsNullOrWhiteSpace(agentTask.LastGeneratedContent) ?
							"Sub-agent did not generate any content." : agentTask.LastGeneratedContent);
					ctx.Call.UseMarkdown = true;
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

		private StreamingToolAnalysisResult DescribeImageStreaming(
			IToolExecutionContext ctx,
			string? path)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path}**");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult DescribeImagePreview(IToolExecutionContext ctx,
			string path, [SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path}**");

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask | ToolBehaviour.FileRead |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private async Task<ToolExecutionResult> DescribeImage(
			[SharedContext] string? fullPath,
			[Description("The title of the agent call to be visible in UI")] string? callTitle,
			[Description("The path to the image file to describe")] string path,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path}**");
			ctx.Call.UseMarkdown = true;

			var modelName = _chatSettings.Settings.Models.GetEffectiveSelection().VisionModel;
			if (string.IsNullOrEmpty(modelName))
			{
				ctx.Call.SetResult($"No vision model selected. Say user to select a vision model first.");
				return ToolExecutionResult.Failed;
			}

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return ToolExecutionResult.Failed;
			}

			LLModel llm;
			try
			{
				llm = _modelManager.GetModel(modelName);
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Vision model '{modelName}' is not available: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);
				var image = Image.Load(fullPath);
				using var memstream = new MemoryStream();
				image.SaveAsPng(memstream);
				var format = "png";
				var base64 = Convert.ToBase64String(memstream.ToArray());
				var url = "data:image/" + format + ";base64," + base64;

				var agentTask = _agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = callTitle,
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					Model = llm,
					InitialMessages = [
						new AgentSystemMessage { Content = _templates.GetTextTemplate("image_describer_prompt").Render().ToString()! },
						new AgentUserMessage {
							Content = "Please describe the image.",
							Attachments = [new AgentNativeAttachment
							{
								Type = AttachmentKind.Image,
								Url = url
							}]
						}
					]
				}, cancellationToken);

				ctx.Call.SetResult(agentTask.LastGeneratedContent ?? string.Empty);

				int tokenCounter = 0;
				void LastResponseChanged(object? sender, PropertyChangedEventArgs e)
				{
					ctx.Call.SetResult(agentTask.LastGeneratedContent ?? string.Empty);
					tokenCounter++;

					if (tokenCounter > 1)
						ctx.Call.SetStatusTitle(string.Format(LocalizationManager.LocalizeStatic("tool.status.agent-describe_image.status"), tokenCounter));
				}
				agentTask.PropertyChanged += LastResponseChanged;

				try
				{
					await agentTask;
				}
				finally
				{
					agentTask.PropertyChanged -= LastResponseChanged;
				}

				ctx.Call.SetStatusTitle(null);
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.AppendResultLine($"Got error: {ex.Message}. " +
					$"May be the model is not a vision model or API is down. Please try again later.");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
