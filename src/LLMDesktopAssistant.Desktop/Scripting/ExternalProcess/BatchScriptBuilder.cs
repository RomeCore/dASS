using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// Builds the batch document: the echo and expansion setup, one <c>set</c> per variable, then the body.
	/// </summary>
	/// <remarks>
	/// Batch holds no structures, so a variable is always the JSON text of its value. The escaping rules are hard
	/// facts of the cmd parser, not choices: inner double quotes survive <c>set "name=value"</c>, <c>%</c> expands
	/// even inside quotes and therefore has to be doubled, and non-ASCII is avoided entirely by serialising with the
	/// encoder's default (ASCII-only) escaping, which makes the value code-page independent.
	/// </remarks>
	public static class BatchScriptBuilder
	{
		/// <summary>
		/// The longest line cmd can hold. A longer one is refused instead of truncated, because a truncated value is
		/// silent corruption of what the addon asked for.
		/// </summary>
		public const int MaxLineLength = 8191;

		// The default encoder escapes everything outside ASCII as \uXXXX, which is the point: a .bat is read in the
		// console code page, so a readable Cyrillic literal would be mojibake on a machine with another code page.
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			Encoder = JavaScriptEncoder.Default
		};

		/// <summary>
		/// Builds the whole script for the given invocation.
		/// </summary>
		/// <param name="parameters">The invocation parameters.</param>
		/// <returns>The script text, ready to be written with CRLF endings.</returns>
		/// <exception cref="InvalidOperationException">A preamble value does not fit into one cmd line.</exception>
		public static string Build(ExternalProcessScriptTerminalParameters parameters)
		{
			var builder = new StringBuilder();

			// Without this the command lines themselves land on stdout, next to whatever the script prints.
			builder.Append("@echo off\r\n");

			// /d already suppresses the machine-wide AutoRun, and this keeps the claim local: a value that contains
			// '!' must stay literal, which only holds while delayed expansion is off.
			builder.Append("setlocal DisableDelayedExpansion\r\n");

			foreach (var (name, value) in parameters.Variables)
			{
				var json = value.ToJsonNode()?.ToJsonString(_jsonOptions) ?? "null";
				var line = $"set \"{name}={json.Replace("%", "%%")}\"";
				if (line.Length > MaxLineLength)
				{
					throw new InvalidOperationException(
						$"The '{name}' preamble value is {line.Length} characters long, which is more than the " +
						$"{MaxLineLength}-character limit of a batch line. cmd would truncate it, so the script is " +
						"not run at all.");
				}

				builder.Append(line).Append("\r\n");
			}

			builder.Append(NormalizeLineEndings(parameters.Body));
			return builder.ToString();
		}

		/// <summary>
		/// Normalises line endings to CRLF: cmd misparses an LF-only file, losing labels and multi-line blocks.
		/// </summary>
		/// <param name="body">The text to normalise.</param>
		/// <returns>The text with CRLF endings only.</returns>
		public static string NormalizeLineEndings(string body)
			=> body.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
	}
}
