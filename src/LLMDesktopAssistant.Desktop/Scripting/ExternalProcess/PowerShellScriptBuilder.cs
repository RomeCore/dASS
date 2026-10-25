using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// Builds the PowerShell document: the console encoding guard, one assignment per variable, then the body.
	/// </summary>
	/// <remarks>
	/// A value is a single-quoted literal with the apostrophe doubled, which is the one form that survives every
	/// character a JSON string can hold. The document itself is UTF-8 with a BOM, so unlike bash the JSON can keep its
	/// Cyrillic readable instead of escaping it.
	/// </remarks>
	public static class PowerShellScriptBuilder
	{
		// The script's reader is PowerShell, not HTML: escaping apostrophes and Cyrillic buys nothing and makes an
		// inspected script unreadable. Single quoting and the JSON grammar keep the value safe.
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		/// <summary>
		/// Builds the whole script for the given invocation.
		/// </summary>
		/// <param name="parameters">The invocation parameters.</param>
		/// <param name="inputMode">The resolved input mode (never <see cref="ScriptInputMode.Auto"/>).</param>
		/// <returns>The script text, ready to be written with CRLF endings.</returns>
		public static string Build(ExternalProcessScriptTerminalParameters parameters, ScriptInputMode inputMode)
		{
			var builder = new StringBuilder();

			// Windows PowerShell 5.1 decodes a redirected stdout stream with the console code page, so the preamble
			// fixes it before the body can print. PowerShell 7 already speaks UTF-8 and on Unix there is no code
			// page to change, so the guard is a version test rather than a platform test.
			builder.Append("if ($PSVersionTable.PSVersion.Major -le 5) { [Console]::OutputEncoding = [Text.Encoding]::UTF8 }\r\n");

			foreach (var (name, value) in parameters.Variables)
			{
				var json = value.ToJsonNode()?.ToJsonString(_jsonOptions) ?? "null";
				var assignment = $"${name} = {SingleQuoted(json)}";
				builder.Append(inputMode is ScriptInputMode.Json ? assignment : assignment + " | ConvertFrom-Json");
				builder.Append("\r\n");
			}

			builder.Append(NormalizeLineEndings(parameters.Body));
			return builder.ToString();
		}

		/// <summary>
		/// Quotes a value as a single PowerShell literal, escaping an apostrophe by doubling it.
		/// </summary>
		/// <param name="value">The text to quote.</param>
		/// <returns>The quoted literal.</returns>
		public static string SingleQuoted(string value) => $"'{value.Replace("'", "''")}'";

		/// <summary>
		/// Normalises line endings to CRLF, which every host parses the same way and which keeps the file readable in
		/// a Windows editor.
		/// </summary>
		/// <param name="body">The text to normalise.</param>
		/// <returns>The text with CRLF endings only.</returns>
		public static string NormalizeLineEndings(string body)
			=> body.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
	}
}
