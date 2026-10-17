using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: true)]
	public class SkillToolModule : ToolModule
	{
		private readonly IAgentManagementService _agentManager;
		private readonly IAddonSetCollector<SkillInfo> _skillsetBuilder;

		public SkillToolModule(IAgentManagementService agentManager,
			IAddonSetCollector<SkillInfo> skillsetBuilder)
		{
			_agentManager = agentManager;
			_skillsetBuilder = skillsetBuilder;

			AddTool(new ToolInitializationInfo
			{
				Executor = LoadSkill,
				Name = "skill-load",
				IsFixed = true,
				Description = "Loads a skill (SKILL.md format) by its name.",
				NameKey = Locale.GetKey("tool.name.skill-load"),
				DescriptionKey = Locale.GetKey("tool.description.skill-load"),
				CategoryKey = Locale.GetKey("tool.category.skills"),
				DefaultExpectedBehaviour = ToolBehaviour.None
			});
		}

		private ToolExecutionResult LoadSkill(
			[Description("The name of the skill to load.")] string name,
			IToolExecutionContext ctx)
		{
			var senderAgent = ctx is ChatToolExecutionContext chatContext
				? _agentManager.TryGetSenderAgentDescriptor(chatContext.Message)
				: null;
			var skills = senderAgent is not null ?
				_skillsetBuilder.GetAddonsForAgent(senderAgent) :
				_skillsetBuilder.GetAvailableAddons();
			var foundSkill = skills.FirstOrDefault(s => s.Name == name);

			if (foundSkill == null)
			{
				ctx.Call.SetResult($"No skill found with the name *{name}*.");
				ctx.Call.SetStatus(MaterialIconKind.Cards, $"*{name}*");
				ctx.Call.UseMarkdown = true;
				return ToolExecutionResult.Failed;
			}

			var body = foundSkill.BodyGetter(foundSkill);

			ctx.Call.SetResult(string.IsNullOrEmpty(foundSkill.HomeDirectory) ? body : $"""
				{body}

				---

				**Note**: all paths in the skill are relative to skill's home path: *{foundSkill.HomeDirectory}*
				""");
			ctx.Call.SetStatus(MaterialIconKind.Cards, $"*{name}*");
			ctx.Call.UseMarkdown = true;
			return ToolExecutionResult.Succeeded;
		}
	}
}
