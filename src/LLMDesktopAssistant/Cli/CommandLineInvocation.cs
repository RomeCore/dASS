namespace LLMDesktopAssistant.Cli
{
	/// <summary>
	/// The result of parsing the process command line: an optional action to execute instead of
	/// starting the GUI, its positional arguments, and the parameters shared by every mode.
	/// </summary>
	/// <param name="Action">The requested action name, or <see langword="null"/> to start the GUI.</param>
	/// <param name="Arguments">The positional arguments left after the action and the flags.</param>
	/// <param name="Parameters">The options that apply to every work mode.</param>
	/// <param name="ShowHelp">True when the usage text was requested.</param>
	/// <param name="ShowVersion">True when the application version was requested.</param>
	public sealed record CommandLineInvocation(
		string? Action,
		IReadOnlyList<string> Arguments,
		CommandLineParameters Parameters,
		bool ShowHelp = false,
		bool ShowVersion = false)
	{
		/// <summary>
		/// The invocation used when nothing has been parsed yet: no action, no arguments.
		/// </summary>
		public static CommandLineInvocation Empty { get; } = new(
			null,
			[],
			new CommandLineParameters());
	}
}
