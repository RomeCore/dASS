using System.Text;
using LLMDesktopAssistant.Desktop.Execution;

namespace LLMDesktopAssistant.Desktop.Tests.Execution;

public class CommandLineEscapingTests
{
	[Theory]
	[InlineData("simple", "simple")]
	[InlineData("with space", "\"with space\"")]
	[InlineData("", "\"\"")]
	[InlineData("back\\slash", "back\\slash")]
	[InlineData("C:\\My Folder\\", "\"C:\\My Folder\\\\\"")]
	[InlineData("\"quote", "\"\\\"quote\"")]
	[InlineData("quote\"inside", "\"quote\\\"inside\"")]
	[InlineData("tab\there", "\"tab\there\"")]
	public void EscapeArgument_MatchesWindowsRules(string argument, string expected)
	{
		Assert.Equal(expected, CommandLineEscaping.EscapeArgument(argument));
	}

	[Fact]
	public void EscapeArguments_JoinsWithSpaces()
	{
		var commandLine = CommandLineEscaping.EscapeArguments(["-Command", "Write-Host \"hi there\""]);

		Assert.Equal("-Command \"Write-Host \\\"hi there\\\"\"", commandLine);
	}

	public static IEnumerable<object[]> RoundTripCases()
	{
		yield return [new[] { "a", "b" }];
		yield return [new[] { "" }];
		yield return [new[] { "", "" }];
		yield return [new[] { "C:\\Program Files\\app.exe", "-x", "a b c" }];
		yield return [new[] { "trailing\\", "C:\\dir\\" }];
		yield return [new[] { "say \"hi\"", "\\\"quoted\\\"" }];
		yield return [new[] { "\"" }];
		yield return [new[] { "a\\\\\"b", "tab\there", "  padded  " }];
		yield return [new[] { "多字节", "emoji 😀", "unicode" }];
		yield return [new[] { "-Command", "Get-ChildItem 'C:\\My Folder' | Where-Object { $_.Name -like \"*.txt\" }" }];
		yield return [new[] { "\\\\server\\share\\my file.txt", "--flag=value with=spaces" }];
		yield return [new[] { "a b", "c\"d", "e\\f", "g\\\\h", "\\", "\\\\", "\\\"" }];
	}

	[Theory]
	[MemberData(nameof(RoundTripCases))]
	public void EscapeArguments_RoundTripsThroughCommandLineToArgvW(string[] arguments)
	{
		var commandLine = CommandLineEscaping.EscapeArguments(arguments);

		Assert.Equal(arguments, ParseCommandLine(commandLine));
	}

	/// <summary>
	/// Reference implementation of the Windows <c>CommandLineToArgvW</c> parsing rules (for
	/// non-argv[0] arguments), used to independently verify the escaping round-trips.
	/// </summary>
	private static string[] ParseCommandLine(string commandLine)
	{
		var arguments = new List<string>();
		var builder = new StringBuilder();
		var i = 0;
		var inQuotes = false;

		SkipWhitespace();
		while (i < commandLine.Length)
		{
			inQuotes = false;
			builder.Clear();

			while (i < commandLine.Length)
			{
				var c = commandLine[i];

				if (c == '\\')
				{
					var backslashes = 0;
					while (i < commandLine.Length && commandLine[i] == '\\')
					{
						backslashes++;
						i++;
					}

					if (i < commandLine.Length && commandLine[i] == '"')
					{
						builder.Append('\\', backslashes / 2);
						if (backslashes % 2 == 1)
						{
							// Odd count escapes the quote into a literal.
							builder.Append('"');
							i++;
						}
						else
						{
							ConsumeQuoteDelimiter();
						}
					}
					else
					{
						builder.Append('\\', backslashes);
					}
					continue;
				}

				if (c == '"')
				{
					ConsumeQuoteDelimiter();
					continue;
				}

				if (IsWhitespace(c) && !inQuotes)
					break;

				builder.Append(c);
				i++;
			}

			arguments.Add(builder.ToString());
			SkipWhitespace();
		}

		return [.. arguments];

		void ConsumeQuoteDelimiter()
		{
			if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
			{
				// Post-2008 rule: a quote followed by another quote is a literal quote.
				builder.Append('"');
				i += 2;
				return;
			}

			inQuotes = !inQuotes;
			i++;
		}

		void SkipWhitespace()
		{
			while (i < commandLine.Length && IsWhitespace(commandLine[i]))
				i++;
		}
	}

	private static bool IsWhitespace(char c) => c is ' ' or '\t';
}
