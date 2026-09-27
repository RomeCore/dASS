using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new SystemReminderSection(services)
			});
		}
	}
}
