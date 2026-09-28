using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new SkillsSection(services)
			});
		}
	}
}
