using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Services.Configurators
{
	[ServiceConfigurator(ServiceScope.App)]
	public class AppNativeSlashCommandProvidersConfigurator : ServiceConfigurator
	{
		public override void Configure(IServiceCollection services)
		{
			var providers = ReflectionUtility.GetTypesWithAttribute<NativeSlashCommandProvider, NativeSlashCommandProviderAttribute>();
			foreach (var provider in providers)
			{
				if (!provider.Attribute.ChatScoped)
					services.AddSingleton(typeof(ISlashCommandProvider), provider.Type);
			}
		}
	}
}
