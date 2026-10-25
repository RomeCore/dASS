using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// Builds the bash document: one single-quoted assignment per variable, then the body.
	/// </summary>
	/// <remarks>
	/// Bash holds no structures, so a variable is always the JSON text of its value and the script decodes it itself.
	/// The base engine rejects <c>json-input: false</c> for this language before this class is reached.
	/// </remarks>
	public static class BashScriptBuilder
	{
		// The script's reader is a JSON parser, not HTML, so escaping apostrophes and Cyrillic buys nothing and
		// makes a printed script unreadable. Single quoting and the JSON grammar keep the value safe.
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		/// <summary>
		/// Builds the whole script for the given invocation.
		/// </summary>
		/// <param name="parameters">The invocation parameters.</param>
		/// <returns>The script text, ready to be written with LF endings.</returns>
		public static string Build(ExternalProcessScriptTerminalParameters parameters)
		{
			var builder = new StringBuilder();

			foreach (var (name, value) in parameters.Variables)
			{
				var json = value.ToJsonNode()?.ToJsonString(_jsonOptions) ?? "null";
				builder.Append(name).Append('=').Append(SingleQuoted(json)).Append('\n');
			}

			builder.Append(NormalizeLineEndings(parameters.Body));
			return builder.ToString();
		}

		/// <summary>
		/// Quotes a value as a single bash literal, escaping an apostrophe the only way bash allows.
		/// </summary>
		/// <param name="value">The text to quote.</param>
		/// <returns>The quoted literal.</returns>
		public static string SingleQuoted(string value) => $"'{value.Replace("'", "'\\''")}'";

		/// <summary>
		/// Normalises line endings to LF: a CRLF script under MSYS fails with <c>$'\r': command not found</c>.
		/// </summary>
		/// <param name="body">The text to normalise.</param>
		/// <returns>The text with LF endings only.</returns>
		public static string NormalizeLineEndings(string body) => body.Replace("\r\n", "\n").Replace('\r', '\n');
	}
}
