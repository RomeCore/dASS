using LLMDesktopAssistant.LLM.Services;

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
				Description = string.Empty,
				IsFixed = true,
				Provider = new IdentitySection(services)
			});
		}
	}
}
