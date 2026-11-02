using Serilog;

namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// The platform-specific halves of the shared initialization: the logger sinks and the stages
	/// that exist only on this platform. Everything else lives in <see cref="SharedInitialization"/>
	/// and <see cref="App"/>.
	/// </summary>
	public interface IPlatformInitialization
	{
		/// <summary>
		/// Adds the platform sinks to the logger configuration: the console on the desktop, logcat
		/// on Android. Called before the first log entry, so it cannot depend on services.
		/// </summary>
		/// <param name="configuration">The configuration built by <see cref="SharedInitialization"/>.</param>
		/// <returns>The same configuration with the platform sinks added.</returns>
		LoggerConfiguration ConfigureLogger(LoggerConfiguration configuration);

		/// <summary>
		/// Creates the stages that exist only on this platform, such as plugin loading.
		/// They run before the assembly scan, so anything they register is scanned.
		/// </summary>
		/// <returns>The platform stages, or an empty list when the platform has none.</returns>
		IReadOnlyList<IStartupStage> CreatePlatformStages();
	}
}
