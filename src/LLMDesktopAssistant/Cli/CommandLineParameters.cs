using LLMDesktopAssistant.Utils;
using Serilog.Events;

namespace LLMDesktopAssistant.Cli
{
	/// <summary>
	/// The options that apply to every work mode, both the GUI startup and any headless action.
	/// </summary>
	public sealed class CommandLineParameters
	{
		/// <summary>
		/// The minimum level written to the log sinks.
		/// </summary>
		public LogEventLevel LogLevel { get; set; } =  DebugHelper.IsDebug ?
			LogEventLevel.Debug : LogEventLevel.Information;

		/// <summary>
		/// True when the startup splash screen must not be shown.
		/// </summary>
		public bool NoSplash { get; set; } = false;
	}
}
