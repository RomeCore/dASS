using System.Text;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Splits a command line string into the argument vector a process will receive.
	/// </summary>
	/// <remarks>
	/// A raw command line is a convenience for scripts, not the canonical form: the array of arguments is,
	/// because it needs no quoting rules at all. The rules implemented here are the common subset of the
	/// Windows and POSIX conventions - single and double quotes group, a backslash escapes the enclosing
	/// quote, whitespace outside quotes separates. Windows path backslashes are therefore left untouched.
	/// </remarks>
	public static class RpcCommandLine
	{
		/// <summary>
		/// Splits the given command line.
		/// </summary>
		/// <param name="commandLine">The command line to split.</param>
		/// <returns>The argument vector, empty for a blank line.</returns>
		/// <exception cref="FormatException">A quote was opened and never closed.</exception>
		public static IReadOnlyList<string> Split(string commandLine)
		{
			var result = new List<string>();

			if (string.IsNullOrWhiteSpace(commandLine))
				return result;

			var current = new StringBuilder();
			var hasToken = false;
			var quote = '\0';

			for (var i = 0; i < commandLine.Length; i++)
			{
				var value = commandLine[i];

				if (quote != '\0')
				{
					// Only the enclosing quote is escapable, so "C:\dir\" keeps its backslashes.
					if (value == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == quote)
					{
						current.Append(quote);
						i++;
						continue;
					}

					if (value == quote)
					{
						quote = '\0';
						continue;
					}

					current.Append(value);
					continue;
				}

				if (value is '"' or '\'')
				{
					quote = value;
					// An empty pair of quotes is an argument, not nothing.
					hasToken = true;
					continue;
				}

				if (char.IsWhiteSpace(value))
				{
					if (hasToken)
					{
						result.Add(current.ToString());
						current.Clear();
						hasToken = false;
					}

					continue;
				}

				current.Append(value);
				hasToken = true;
			}

			if (quote != '\0')
				throw new FormatException($"The command line has an unterminated {quote} quote.");

			if (hasToken)
				result.Add(current.ToString());

			return result;
		}
	}
}
