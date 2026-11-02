using System.Collections.Generic;
using LLMDesktopAssistant.Android.Utils;
using LLMDesktopAssistant.Startup;
using Serilog;

namespace LLMDesktopAssistant.Avalonia.Android
{
	/// <summary>
	/// The Android half of the shared initialization: logcat instead of the console, and no platform
	/// stages, because the platform has neither plugins nor a headless mode.
	/// </summary>
	public sealed class AndroidPlatformInitialization : IPlatformInitialization
	{
		/// <summary>
		/// Gets the single instance of the Android initialization.
		/// </summary>
		public static AndroidPlatformInitialization Instance { get; } = new();

		/// <inheritdoc />
		public LoggerConfiguration ConfigureLogger(LoggerConfiguration configuration)
		{
			return configuration.WriteTo.Sink(new AndroidDebugLogSink());
		}

		/// <inheritdoc />
		public IReadOnlyList<IStartupStage> CreatePlatformStages() => [];
	}
}
