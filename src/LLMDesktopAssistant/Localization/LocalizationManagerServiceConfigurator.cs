using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Localization
{
	[ServiceConfigurator]
	public class LocalizationManagerServiceConfigurator : ServiceConfigurator
	{
		public override void Configure(IServiceCollection services)
		{
			services.AddSingleton(LocalizationManager.Current ?? new LocFileLocalizationManager());
		}
	}
}
