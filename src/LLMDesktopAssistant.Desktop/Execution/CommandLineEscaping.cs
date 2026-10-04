using System.Text;

namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// Encodes an argv sequence into a single Windows command line using the exact rules the
	/// C runtime argument parser (and <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>)
	/// uses. This exists because <c>Porta.Pty</c> only implements cmd.exe-style quoting
	/// (<c>"..."</c> with <c>""</c> doubling), which corrupts embedded quotes, silently drops
	/// empty arguments and breaks arguments that end with a backslash for ordinary processes.
	/// </summary>
	public static class CommandLineEscaping
	{
		/// <summary>
		/// Escapes each argument and joins them with spaces into a Windows command line.
		/// </summary>
		public static string EscapeArguments(IEnumerable<string> arguments)
		{
			var builder = new StringBuilder();
			var first = true;
			foreach (var argument in arguments)
			{
				if (!first)
					builder.Append(' ');
				first = false;
				AppendArgument(builder, argument);
			}
			return builder.ToString();
		}

		/// <summary>
		/// Escapes a single argument so that it survives Windows command line parsing unchanged.
		/// </summary>
		public static string EscapeArgument(string argument)
		{
			var builder = new StringBuilder();
			AppendArgument(builder, argument);
			return builder.ToString();
		}

		// Ported verbatim from System.PasteArguments.AppendArgument (dotnet/runtime) so that the
		// terminal path produces byte-identical argument lines to ProcessStartInfo.ArgumentList.
		//
		// Parsing rules for non-argv[0] arguments:
		//   - Backslash is a normal character except when followed by a quote.
		//   - 2N backslashes followed by a quote  => N literal backslashes followed by a delimiter.
		//   - 2N+1 backslashes followed by a quote => N literal backslashes followed by a literal quote.
		//   - Parsing stops at the first whitespace outside a quoted region.
		//   - (post 2008 rule): a closing quote followed by another quote is a literal quote and
		//     quoting continues.
		private static void AppendArgument(StringBuilder builder, string argument)
		{
			if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
			{
				// Simple case - no quoting or changes needed.
				builder.Append(argument);
				return;
			}

			builder.Append('"');
			var idx = 0;
			while (idx < argument.Length)
			{
				var c = argument[idx++];
				if (c == '\\')
				{
					var numBackSlash = 1;
					while (idx < argument.Length && argument[idx] == '\\')
					{
						idx++;
						numBackSlash++;
					}

					if (idx == argument.Length)
					{
						// We'll emit an end quote after this so must double the number of backslashes.
						builder.Append('\\', numBackSlash * 2);
					}
					else if (argument[idx] == '"')
					{
						// Backslashes will be followed by a quote. Must double the number of backslashes.
						builder.Append('\\', numBackSlash * 2 + 1);
						builder.Append('"');
						idx++;
					}
					else
					{
						// Backslash will not be followed by a quote, so emit as normal characters.
						builder.Append('\\', numBackSlash);
					}
					continue;
				}

				if (c == '"')
				{
					// Escape the quote so it appears as a literal. This also guarantees that we won't end up
					// generating a closing quote followed by another quote (which parses differently
					// pre-2008 vs. post-2008.)
					builder.Append('\\');
					builder.Append('"');
					continue;
				}

				builder.Append(c);
			}
			builder.Append('"');
		}

		private static bool ContainsNoWhitespaceOrQuotes(string s)
		{
			foreach (var c in s)
			{
				if (char.IsWhiteSpace(c) || c == '"')
					return false;
			}
			return true;
		}
	}
}
