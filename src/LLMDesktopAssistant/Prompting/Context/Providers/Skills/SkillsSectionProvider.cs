using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class SkillsSectionProvider : PromptContextNativeProvider
	{
		public SkillsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "skills",
				Order = 20,
				Description = "The skills available to the agent, with the description, location and body of every skill.",
				NameKey = Locale.GetKey("prompt.context.name.skills"),
				DescriptionKey = Locale.GetKey("prompt.context.description.skills"),
				IsFixed = true,
				Provider = new SkillsSection(services)
			});
		}
	}
}
