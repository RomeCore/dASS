using LLMDesktopAssistant.Cli;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Startup
{
	/// <summary>
	/// The part of the startup that every host performs before anything else and that is too cheap
	/// to become a stage: the directories, the logger and the localization singleton.
	/// </summary>
	public static class SharedInitialization
	{
		/// <summary>
		/// Gets the platform that supplies the logger sinks and the platform stages.
		/// </summary>
		public static IPlatformInitialization Platform { get; private set; } = null!;

		/// <summary>
		/// Initializes everything that must exist before any stage runs. Must be called once per
		/// process, before the application object is created.
		/// </summary>
		/// <param name="args">The raw process arguments, logged for diagnostics.</param>
		/// <param name="platform">The platform-specific initialization.</param>
		public static void Initialize(string[] args, IPlatformInitialization platform)
		{
			ArgumentNullException.ThrowIfNull(platform);

			Platform = platform;

			Directories.ClearTempFiles();
			Directories.EnsureAll();

			var configuration = new LoggerConfiguration()
				.MinimumLevel.Is(CommandLineExecutor.Parameters.LogLevel);

			Log.Logger = platform
				.ConfigureLogger(configuration)
				.WriteTo.File(
					Path.Combine(Directories.LogFiles, "log.txt"),
					rollingInterval: RollingInterval.Day)
				.CreateLogger();

			Log.Debug("Command line: {Arguments}", string.Join(' ', args));

			// These are read from their own file on purpose: the settings manager cannot be touched
			// before the reflection scan, and the localization below needs the saved language.
			ApplicationStartupSettingsAccessor.Load();

			// The localization singleton is deliberately not a service: XAML needs it long before
			// the container exists, and it has no dependencies of its own.
			LocalizationManager.SetOverrideManager(new LocFileLocalizationManager());
		}
	}
}
