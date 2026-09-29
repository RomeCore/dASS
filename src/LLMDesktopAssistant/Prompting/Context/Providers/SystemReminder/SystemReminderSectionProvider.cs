using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Reminder
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class SystemReminderSectionProvider : PromptContextNativeProvider
	{
		public SystemReminderSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "system-reminder",
				Order = int.MaxValue,
				Description = "Explains the <system-reminder> tags: how changes of the prompt and of the context are announced to the agent.",
				NameKey = Locale.GetKey("prompt.context.name.system-reminder"),
				DescriptionKey = Locale.GetKey("prompt.context.description.system-reminder"),
				IsFixed = true,
				Provider = new SystemReminderSection(services)
			});
		}
	}
}
