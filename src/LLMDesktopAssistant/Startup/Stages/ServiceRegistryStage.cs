using Avalonia;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Serilog;

namespace LLMDesktopAssistant.Startup.Stages
{
	/// <summary>
	/// The stage that builds the application container and then constructs every registered service,
	/// publishing each one so the splash can show a live counter.
	/// </summary>
	public sealed class ServiceRegistryStage : IStartupStage
	{
		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("splash.stage.services");

		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken = default)
		{
			var application = Application.Current as App
				?? throw new InvalidOperationException("The application instance is not available yet.");

			ServiceRegistry.Build([Log.Logger], application.ConfigureServicesForStartup);

			ServiceRegistry.WarmUp((descriptor, index, count) =>
			{
				cancellationToken.ThrowIfCancellationRequested();

				StartupSplashManager.SetStageProgress(
					(double)(index + 1) / count,
					Locale.GetFormattedKey(
						"splash.loading_module",
						descriptor.ServiceType.Name,
						$"{index + 1}/{count}"));
			});

			return Task.CompletedTask;
		}
	}
}
