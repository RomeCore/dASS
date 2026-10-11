using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Services.Configurators
{
	[ServiceConfigurator(ServiceScope.Chat)]
	public class ChatNativeSlashCommandProvidersConfigurator : ServiceConfigurator
	{
		public override void Configure(IServiceCollection services)
		{
			var providers = ReflectionUtility.GetTypesWithAttribute<NativeSlashCommandProvider, NativeSlashCommandProviderAttribute>();
			foreach (var provider in providers)
			{
				if (provider.Attribute.ChatScoped)
					services.AddScoped(typeof(ISlashCommandProvider), provider.Type);
			}
		}
	}
}
