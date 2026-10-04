using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
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

		private ReactiveToolResult LoadSkill(
			[Description("The name of the skill to load.")] string name,
			ToolExecutionContext ctx)
		{
			var senderAgent = _agentManager.TryGetSenderAgentDescriptor(ctx.Message);
			var skills = senderAgent is not null ?
				_skillsetBuilder.GetAddonsForAgent(senderAgent) :
				_skillsetBuilder.GetAvailableAddons();
			var foundSkill = skills.FirstOrDefault(s => s.Name == name);

			if (foundSkill == null)
			{
				return new ReactiveToolResult
				{
					StatusIcon = MaterialIconKind.Cards,
					StatusTitle = $"*{name}*",
					ResultContent = $"No skill found with the name *{name}*.",
					UseMarkdown = true
				}.CompleteWithError();
			}

			var body = foundSkill.BodyGetter(foundSkill);

			return new ReactiveToolResult
			{
				StatusIcon = MaterialIconKind.Cards,
				StatusTitle = $"*{name}*",
				ResultContent = string.IsNullOrEmpty(foundSkill.HomeDirectory) ? body : $"""
				{body}

				---

				**Note**: all paths in the skill are relative to skill's home path: *{foundSkill.HomeDirectory}*
				""",
				UseMarkdown = true
			}.CompleteWithSuccess();
		}
	}
}
