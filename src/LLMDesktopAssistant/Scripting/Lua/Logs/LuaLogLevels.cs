using Serilog.Events;

namespace LLMDesktopAssistant.Scripting.Lua.Logs
{
	/// <summary>
	/// The level names the Lua side uses, mapped to <see cref="LogEventLevel"/>.
	/// </summary>
	public static class LuaLogLevels
	{
		/// <summary>
		/// The accepted level names, as the manuals and the errors list them.
		/// </summary>
		public const string Names = "verbose, debug, info, warning, error, fatal";

		private static readonly Dictionary<string, LogEventLevel> _levels = new(StringComparer.OrdinalIgnoreCase)
		{
			["verbose"] = LogEventLevel.Verbose,
			["debug"] = LogEventLevel.Debug,
			["info"] = LogEventLevel.Information,
			["information"] = LogEventLevel.Information,
			["warn"] = LogEventLevel.Warning,
			["warning"] = LogEventLevel.Warning,
			["error"] = LogEventLevel.Error,
			["fatal"] = LogEventLevel.Fatal,
		};

		/// <summary>
		/// Resolves a level name.
		/// </summary>
		/// <param name="name">The name to resolve, case-insensitive.</param>
		/// <param name="level">The level it names.</param>
		/// <returns><see langword="true"/> when the name is known.</returns>
		public static bool TryParse(string name, out LogEventLevel level)
			=> _levels.TryGetValue(name, out level);

		/// <summary>
		/// The canonical name of a level, as entries report it.
		/// </summary>
		public static string Name(LogEventLevel level) => level switch
		{
			LogEventLevel.Verbose => "verbose",
			LogEventLevel.Debug => "debug",
			LogEventLevel.Information => "info",
			LogEventLevel.Warning => "warning",
			LogEventLevel.Error => "error",
			LogEventLevel.Fatal => "fatal",
			_ => "unknown",
		};
	}
}
