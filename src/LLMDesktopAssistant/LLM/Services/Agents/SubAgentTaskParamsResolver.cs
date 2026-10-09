using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.Services.Agents
{
	[ChatService(typeof(ISubAgentTaskParamsResolver))]
	public class SubAgentTaskParamsResolver(
		IChatSettingsService chatSettings,
		ISubAgentToolResolver toolResolver,
		IAddonSetCollector<SkillInfo> skillsetBuilder,
		IAddonSetCollector<SubAgentInfo> subAgentsetCollector
	) : ISubAgentTaskParamsResolver
	{
		public AgentTaskLaunchParameters Resolve(AgentTaskLaunchParameters sourceParameters,
			TaskSubAgentDescriptor descriptor, IEnumerable<AgentChatMessage> additionalMessages, out List<string> errors,
			ToolPolicyMask? policyOverride = null)
		{
			errors = [];

			var chatSettingsObj = chatSettings.Settings;

			// The chat-level sub-agent policy replaces the caller's policy when provided.
			var autoApproveBehaviours = policyOverride?.AutoApproveBehaviours ?? sourceParameters.AutoApproveBehaviours;
			var disallowedBehaviours = policyOverride?.DisallowedBehaviours ?? sourceParameters.DisallowedBehaviours;

			if (descriptor is DirectTaskSubAgentDescriptor directDescriptor)
			{
				return new AgentTaskLaunchParameters
				{
					TaskName = descriptor.Name,
					TriggeredChat = sourceParameters.TriggeredChat,
					TriggeredMessage = sourceParameters.TriggeredMessage,

					ModelName = directDescriptor.Model ?? chatSettingsObj.Models.GetEffectiveSelection().AgenticToolsModel,
					Behaviour = AgentTaskExecutionBehaviour.Normal,
					InitialMessages = [
						new AgentSystemMessage { Content = directDescriptor.SystemPrompt },
						..additionalMessages ],

					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours,
					TimeOut = sourceParameters.TimeOut,
					CompletionExpiryTime = sourceParameters.CompletionExpiryTime,
					MaxParallelToolCalls = sourceParameters.MaxParallelToolCalls,
					FeedbackFunc = null,

					Tools = directDescriptor.Tools,
					Skills = directDescriptor.Skills,
					MemoryBlocks = directDescriptor.MemoryBlocks,
					SubAgents = directDescriptor.SubAgents
				};
			}

			var subAgentsMap = subAgentsetCollector.GetAvailableAddons().ToDictionary(s => s.Name);

			var info = subAgentsMap.GetValueOrDefault(descriptor.Name)
				?? throw new KeyNotFoundException($"Sub-agent '{descriptor.Name}' not found.");

			var tools = toolResolver.ResolveSubAgentTools(info, out var toolErrors);

			var skills = ImmutableList.CreateBuilder<AgentSkill>();
			if (info.Skills.Count > 0)
			{
				var skillMap = skillsetBuilder.GetAvailableAddons().ToDictionary(s => s.Name);

				foreach (var allowedSkill in info.Skills.Distinct())
				{
					if (skillMap.TryGetValue(allowedSkill, out var skillInfo))
					{
						skills.Add(new ChatAgentSkill(skillInfo));
					}
					else
					{
						errors.Add("Skill was not found: " + allowedSkill);
					}
				}
			}

			var memoryBlocks = ImmutableList.CreateBuilder<TaskMemoryBlock>();
			if (info.MemoryBlocks.Count > 0)
			{
				var availableMap = SettingsManager.GetCategory<MemoryBlock>().GetAll().ToDictionary(b => b.Value.Name, b => b.Value);

				foreach (var (blockName, attachmentMode) in info.MemoryBlocks)
				{
					if (availableMap.TryGetValue(blockName, out var block))
					{
						memoryBlocks.Add(new TaskMemoryBlock
						{
							Block = block,
							CanRead = attachmentMode is MemoryBlockAttachmentMode.Standard or MemoryBlockAttachmentMode.ReadOnly,
							CanWrite = attachmentMode is MemoryBlockAttachmentMode.Standard or MemoryBlockAttachmentMode.WriteOnly
						});
					}
					else
					{
						errors.Add("Memory block was not found: " + blockName);
					}
				}
			}

			var subAgents = ImmutableList.CreateBuilder<TaskSubAgentDescriptor>();
			if (info.SubAgents.Count > 0)
			{
				foreach (var subAgent in info.SubAgents)
				{
					if (subAgentsMap.TryGetValue(subAgent, out var subAgentInfo))
					{
						subAgents.Add(new TaskSubAgentDescriptor
						{
							Name = subAgentInfo.Name,
							Description = subAgentInfo.Description
						});
					}
					else
					{
						errors.Add("Sub-agent was not found: " + subAgent);
					}
				}
			}

			return new AgentTaskLaunchParameters
			{
				TaskName = descriptor.Name,
				TriggeredChat = sourceParameters.TriggeredChat,
				TriggeredMessage = sourceParameters.TriggeredMessage,

				ModelName = info.Model ?? chatSettingsObj.Models.GetEffectiveSelection().AgenticToolsModel,
				Behaviour = AgentTaskExecutionBehaviour.Normal,
				InitialMessages = [
					new AgentSystemMessage { Content = info.Body },
						..additionalMessages ],

				AutoApproveBehaviours = autoApproveBehaviours,
				DisallowedBehaviours = disallowedBehaviours,
				TimeOut = sourceParameters.TimeOut,
				CompletionExpiryTime = sourceParameters.CompletionExpiryTime,
				MaxParallelToolCalls = sourceParameters.MaxParallelToolCalls,
				FeedbackFunc = null,

				Tools = [..tools],
				Skills = skills.ToImmutableList(),
				MemoryBlocks = memoryBlocks.ToImmutableList(),
				SubAgents = subAgents.ToImmutableList()
			};
		}
	}
}
