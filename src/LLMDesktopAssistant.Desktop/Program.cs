using Avalonia;
using LLMDesktopAssistant.Cli;
using LLMDesktopAssistant.Desktop.Utils;
using LLMDesktopAssistant.Startup;
using Serilog;

namespace LLMDesktopAssistant.Desktop
{
	internal sealed class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			ConsoleManager.EnsureConsole();

			// The command line is parsed before anything is logged, because it carries the log level.
			CommandLineExecutor.Parse(args);

			SharedInitialization.Initialize(args, DesktopPlatformInitialization.Instance);

			// A headless action has already run and the process must not start the UI.
			if (!CommandLineExecutor.TryExecute(args))
				return;

			try
			{
				BuildAvaloniaApp()
					.StartWithClassicDesktopLifetime(args);
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, "A fatal error occurred: {Message}", ex.Message);
				throw;
			}
		}

		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<DesktopApp>()
				.UsePlatformDetect()
				.WithInterFont()
				.LogToTrace();
	}
}
