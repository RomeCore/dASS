using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Identity
{
	[ChatService(typeof(PromptContextNativeProvider))]
	public class IdentitySectionProvider : PromptContextNativeProvider
	{
		public IdentitySectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "identity",
				Order = 10,
				Description = "The identity of the agent: the persona, the specialization and the assistant nickname.",
				NameKey = Locale.GetKey("prompt.context.name.identity"),
				DescriptionKey = Locale.GetKey("prompt.context.description.identity"),
				IsFixed = true,
				Provider = new IdentitySection(services)
			});
		}
	}
}
