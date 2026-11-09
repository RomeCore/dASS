using System.Text;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Separates the executable from the rest of a command line.
	/// </summary>
	/// <remarks>
	/// A raw command line is a convenience for scripts, not the canonical form: an array of arguments is,
	/// because it needs no quoting rules at all. Only the executable has to be understood here - the process
	/// needs it as its file name - while everything after it is handed to the child verbatim, so the quoting
	/// the caller wrote is what the program parses. Single and double quotes group, a backslash escapes the
	/// enclosing quote, and whitespace outside quotes separates. Windows path backslashes are left untouched.
	/// </remarks>
	public static class RpcCommandLine
	{
		/// <summary>
		/// Splits a command line into its executable and the arguments that follow it.
		/// </summary>
		/// <param name="commandLine">The command line to split.</param>
		/// <returns>
		/// The executable with its quotes removed and the remaining text, untouched apart from leading
		/// whitespace; both are empty for a blank line.
		/// </returns>
		/// <exception cref="FormatException">The executable opened a quote and never closed it.</exception>
		public static (string Executable, string Arguments) SplitExecutable(string commandLine)
		{
			if (string.IsNullOrWhiteSpace(commandLine))
				return (string.Empty, string.Empty);

			var executable = new StringBuilder();
			var quote = '\0';
			var index = 0;

			for (; index < commandLine.Length; index++)
			{
				var value = commandLine[index];

				if (quote != '\0')
				{
					// Only the enclosing quote is escapable, so "C:\dir\" keeps its backslashes.
					if (value == '\\' && index + 1 < commandLine.Length && commandLine[index + 1] == quote)
					{
						executable.Append(quote);
						index++;
						continue;
					}

					if (value == quote)
					{
						quote = '\0';
						continue;
					}

					executable.Append(value);
					continue;
				}

				if (value is '"' or '\'')
				{
					quote = value;
					continue;
				}

				if (char.IsWhiteSpace(value))
					break;

				executable.Append(value);
			}

			if (quote != '\0')
				throw new FormatException($"The command line has an unterminated {quote} quote.");

			var arguments = index < commandLine.Length
				? commandLine[(index + 1)..].TrimStart()
				: string.Empty;

			return (executable.ToString(), arguments);
		}
	}
}
