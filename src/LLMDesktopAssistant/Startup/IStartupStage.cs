using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// One named step of the startup sequence. A stage reports its own progress through
	/// <see cref="StartupSplashManager"/>, which is why the interface carries only a title and a body.
	/// </summary>
	public interface IStartupStage
	{
		/// <summary>
		/// The localized title shown while the stage is running.
		/// </summary>
		LocaleKeyBase Title { get; }

		/// <summary>
		/// Runs the stage. Stages are executed on a background thread, so a stage that touches
		/// the UI must marshal to the dispatcher itself.
		/// </summary>
		/// <param name="cancellationToken">The token that cancels the startup sequence.</param>
		Task RunAsync(CancellationToken cancellationToken = default);
	}
}
