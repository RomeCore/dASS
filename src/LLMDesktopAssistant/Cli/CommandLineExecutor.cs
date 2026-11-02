using Serilog.Events;

namespace LLMDesktopAssistant.Cli
{
	/// <summary>
	/// The single owner of the process command line: it parses the shared parameters and decides
	/// whether the process should start the GUI or execute a headless action instead.
	/// </summary>
	public static class CommandLineExecutor
	{
		/// <summary>
		/// The exit code used when the arguments cannot be understood.
		/// </summary>
		public const int InvalidArgumentsExitCode = 2;

		private const string usage = """
			Usage: dass [action] [options]

			Actions:
			  (none)              Start the desktop interface.

			Options:
			  --log-level <level> Minimum log level: trace, debug, info, warn, error, fatal.
			  --no-splash         Do not show the startup splash screen.
			  -h, --help          Show this help and exit.
			  -v, --version       Show the application version and exit.
			""";

		private static readonly Dictionary<string, LogEventLevel> _logLevels = new(StringComparer.OrdinalIgnoreCase)
		{
			["trace"] = LogEventLevel.Verbose,
			["debug"] = LogEventLevel.Debug,
			["info"] = LogEventLevel.Information,
			["warn"] = LogEventLevel.Warning,
			["error"] = LogEventLevel.Error,
			["fatal"] = LogEventLevel.Fatal,
		};

		private static CommandLineInvocation _invocation = CommandLineInvocation.Empty;

		/// <summary>
		/// Gets the parsed command line.
		/// </summary>
		public static CommandLineInvocation Invocation => _invocation;

		/// <summary>
		/// Gets the options that apply to every work mode.
		/// </summary>
		public static CommandLineParameters Parameters => _invocation.Parameters;

		/// <summary>
		/// Parses the arguments and stores the result. Writes the usage text and terminates the
		/// process for <c>--help</c>, <c>--version</c> and malformed input, so it must be called
		/// before any logging is configured.
		/// </summary>
		/// <param name="args">The raw process arguments.</param>
		public static void Parse(string[] args)
		{
			if (!TryParse(args, out var invocation, out var error))
			{
				Console.Error.WriteLine(error);
				Console.Error.WriteLine();
				Console.Error.WriteLine(usage);
				Environment.Exit(InvalidArgumentsExitCode);
				return;
			}

			_invocation = invocation;

			if (invocation.ShowHelp)
			{
				Console.Out.WriteLine(usage);
				Environment.Exit(0);
			}

			if (invocation.ShowVersion)
			{
				Console.Out.WriteLine(App.Version);
				Environment.Exit(0);
			}
		}

		/// <summary>
		/// Executes the requested headless action.
		/// </summary>
		/// <param name="args">The raw process arguments, already parsed by <see cref="Parse"/>.</param>
		/// <returns>
		/// True when the caller must continue with the GUI startup; false when an action has already
		/// been executed and the process must end.
		/// </returns>
		public static bool TryExecute(string[] args)
		{
			// No action is declared yet, so Parse rejects every positional token as unknown.
			// This is the seam a future headless action plugs into.
			if (_invocation.Action is not null)
			{
				Console.Error.WriteLine($"Unknown action '{_invocation.Action}'.");
				Environment.Exit(InvalidArgumentsExitCode);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Parses the arguments without terminating the process. This is the pure half of
		/// <see cref="Parse"/>, which is what the tests use.
		/// </summary>
		/// <param name="args">The raw process arguments.</param>
		/// <param name="invocation">The parsed command line when parsing succeeds.</param>
		/// <param name="error">The human-readable reason when parsing fails, otherwise <see langword="null"/>.</param>
		/// <returns>True when the arguments were understood.</returns>
		internal static bool TryParse(string[] args, out CommandLineInvocation invocation, out string? error)
		{
			invocation = CommandLineInvocation.Empty;
			error = null;

			var parameters = new CommandLineParameters();
			var showHelp = false;
			var showVersion = false;
			string? action = null;
			List<string> arguments = [];

			for (var index = 0; index < args.Length; index++)
			{
				var argument = args[index];

				if (argument.StartsWith("--", StringComparison.Ordinal))
				{
					var separator = argument.IndexOf('=');
					var name = separator < 0 ? argument : argument[..separator];
					var inlineValue = separator < 0 ? null : argument[(separator + 1)..];

					switch (name)
					{
						case "--no-splash":
							if (inlineValue is not null)
							{
								error = "The '--no-splash' option does not take a value.";
								return false;
							}

							parameters.NoSplash = true;
							continue;

						case "--log-level":
							var level = inlineValue;
							if (level is null)
							{
								if (index + 1 >= args.Length)
								{
									error = "The '--log-level' option requires a value.";
									return false;
								}

								level = args[++index];
							}

							if (!_logLevels.TryGetValue(level, out var _logLevel))
							{
								error = $"Unknown log level '{level}'. Expected one of: {string.Join(", ", _logLevels.Keys)}.";
								return false;
							}
							parameters.LogLevel = _logLevel;

							continue;

						case "--help":
							showHelp = true;
							continue;

						case "--version":
							showVersion = true;
							continue;

						default:
							error = $"Unknown option '{name}'.";
							return false;
					}
				}

				if (argument.Length > 1 && argument[0] == '-')
				{
					switch (argument)
					{
						case "-h" or "-?":
							showHelp = true;
							continue;

						case "-v":
							showVersion = true;
							continue;

						default:
							error = $"Unknown option '{argument}'.";
							return false;
					}
				}

				if (action is null)
					action = argument;
				else
					arguments.Add(argument);
			}

			if (action is not null)
			{
				error = $"Unknown action '{action}'.";
				return false;
			}

			invocation = new CommandLineInvocation(
				action,
				arguments,
				parameters,
				showHelp,
				showVersion);
			return true;
		}
	}
}
