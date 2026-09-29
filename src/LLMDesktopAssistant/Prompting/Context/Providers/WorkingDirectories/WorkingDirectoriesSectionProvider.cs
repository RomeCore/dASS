using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// Provides the working directories section. Unlike the other native sections the section is
	/// not fixed: it can be switched off and on per agent.
	/// </summary>
	[ChatService(typeof(PromptContextNativeProvider))]
	public class WorkingDirectoriesSectionProvider : PromptContextNativeProvider
	{
		public WorkingDirectoriesSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "working-directories",
				Order = 50,
				Variability = PromptSectionVariability.Rare,
				Description = "The folders the agent is allowed to work in and which of them is the active root for relative paths, " +
					"including the shell, Python, Lua, the addon environment and some templates.",
				NameKey = Locale.GetKey("prompt.context.name.working-directories"),
				DescriptionKey = Locale.GetKey("prompt.context.description.working-directories"),
				IsFixed = false,
				Provider = new WorkingDirectoriesSection(services)
			});
		}
	}
}
