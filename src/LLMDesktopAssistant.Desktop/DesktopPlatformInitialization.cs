using System.Reflection;
using Avalonia;
using LLMDesktopAssistant.Blazor;
using LLMDesktopAssistant.Desktop.Utils;
using LLMDesktopAssistant.Startup;
using LLMDesktopAssistant.Startup.Stages;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace LLMDesktopAssistant.Desktop
{
	/// <summary>
	/// The desktop half of the shared initialization: the console sinks for the logger and the
	/// stage that loads the plugins.
	/// </summary>
	public sealed class DesktopPlatformInitialization : IPlatformInitialization
	{
		/// <summary>
		/// Gets the single instance of the desktop initialization.
		/// </summary>
		public static DesktopPlatformInitialization Instance { get; } = new();

		/// <inheritdoc />
		public LoggerConfiguration ConfigureLogger(LoggerConfiguration configuration)
		{
			return configuration
				.WriteTo.Sink(new ConsoleAllocatorSink())
				.WriteTo.Console(applyThemeToRedirectedOutput: true, theme: SystemConsoleTheme.Literate);
		}

		/// <inheritdoc />
		public IReadOnlyList<IStartupStage> CreatePlatformStages()
		{
			// The Blazor UI assembly carries services the desktop host needs, so it is scanned
			// together with the plugins.
			return [new PluginsStage(typeof(ChatBlazorUIStarter).Assembly)];
		}
	}
}
