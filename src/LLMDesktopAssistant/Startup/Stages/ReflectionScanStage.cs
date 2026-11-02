using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Startup.Stages
{
	/// <summary>
	/// The stage that walks every loaded assembly and caches its types for the attribute-driven
	/// registrations. This is the single most expensive scan of the startup.
	/// </summary>
	public sealed class ReflectionScanStage : IStartupStage
	{
		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("splash.stage.scan");

		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			ReflectionUtility.Initialize(AppDomain.CurrentDomain);

			return Task.CompletedTask;
		}
	}
}
