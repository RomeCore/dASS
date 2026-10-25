using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// The grammar of a promised JSON: either the whole standard output or a <c>"&lt;BEGIN&gt;*&lt;END&gt;"</c>
	/// pattern, where an unescaped <c>*</c> marks the payload and <c>\*</c> is a literal asterisk.
	/// </summary>
	/// <remarks>
	/// The parser validates a pattern and the engine splits it, so they must agree on what "one unescaped asterisk"
	/// means - that is why the grammar lives here and not in either of them. A backslash escapes only the asterisk
	/// that follows it and is literal everywhere else, so a marker may carry a literal one: <c>@@\*@@*@@</c> finds a
	/// payload between <c>@@*@@</c> and <c>@@</c>.
	/// </remarks>
	public static class ScriptOutputPattern
	{
		/// <summary>
		/// Counts the asterisks a pattern uses as markers (<c>\*</c> is not one).
		/// </summary>
		/// <param name="pattern">The pattern to inspect, or <see langword="null"/>.</param>
		/// <returns>The number of unescaped asterisks.</returns>
		public static int CountUnescapedStars(string? pattern)
		{
			if (string.IsNullOrEmpty(pattern))
				return 0;

			var count = 0;
			for (int i = 0; i < pattern.Length; i++)
			{
				if (pattern[i] is '\\' && i + 1 < pattern.Length && pattern[i + 1] is '*')
				{
					i++;
					continue;
				}

				if (pattern[i] is '*')
					count++;
			}

			return count;
		}

		/// <summary>
		/// Splits a pattern at its first unescaped asterisk.
		/// </summary>
		/// <param name="pattern">The pattern to split.</param>
		/// <param name="begin">The text before the asterisk, with the escapes resolved.</param>
		/// <param name="end">The text after the asterisk, with the escapes resolved.</param>
		/// <returns><see langword="true"/> when the pattern has an unescaped asterisk; otherwise <see langword="false"/>.</returns>
		public static bool TrySplit(string? pattern, out string begin, out string end)
		{
			var beginBuilder = new StringBuilder();
			var endBuilder = new StringBuilder();
			var afterStar = false;

			for (int i = 0; i < (pattern?.Length ?? 0); i++)
			{
				var ch = pattern![i];
				if (ch is '\\' && i + 1 < pattern.Length && pattern[i + 1] is '*')
				{
					(afterStar ? endBuilder : beginBuilder).Append('*');
					i++;
					continue;
				}

				if (ch is '*' && !afterStar)
				{
					afterStar = true;
					continue;
				}

				(afterStar ? endBuilder : beginBuilder).Append(ch);
			}

			begin = beginBuilder.ToString();
			end = endBuilder.ToString();
			return afterStar;
		}

		/// <summary>
		/// Takes the promised structure out of standard output.
		/// </summary>
		/// <remarks>
		/// The capture is greedy: it starts at the first begin marker and ends at the last end marker after it, so an
		/// end marker inside the payload does not cut the payload short.
		/// </remarks>
		/// <param name="stdout">The standard output of the script.</param>
		/// <param name="mode">How the addon promised its JSON.</param>
		/// <param name="pattern">The pattern, when <paramref name="mode"/> is <see cref="ScriptOutputMode.Pattern"/>.</param>
		/// <param name="error">The reason the promise was not kept, or <see langword="null"/>.</param>
		/// <returns>The parsed JSON, or <see langword="null"/> when there is none or the promise was broken.</returns>
		public static JsonNode? Extract(string stdout, ScriptOutputMode mode, string? pattern, out string? error)
		{
			error = null;

			string payload;
			if (mode is ScriptOutputMode.Whole)
			{
				payload = stdout.Trim();
			}
			else
			{
				if (string.IsNullOrEmpty(pattern))
				{
					error = "The addon declares a pattern output mode but no pattern.";
					return null;
				}

				if (!TrySplit(pattern, out var begin, out var end))
				{
					error = $"The output pattern '{pattern}' must contain exactly one unescaped '*'.";
					return null;
				}

				var start = begin.Length == 0 ? 0 : stdout.IndexOf(begin, StringComparison.Ordinal);
				if (start < 0)
				{
					error = $"The output marker '{begin}' was not found in standard output.";
					return null;
				}

				start += begin.Length;

				// Greedy on purpose: a payload may contain the end marker, and cutting at the first one would slice the JSON.
				var finish = end.Length == 0 ? stdout.Length : stdout.LastIndexOf(end, StringComparison.Ordinal);
				if (finish < start)
				{
					error = $"The output marker '{end}' was not found after '{begin}'.";
					return null;
				}

				payload = stdout[start..finish].Trim();
			}

			try
			{
				return JsonNode.Parse(payload);
			}
			catch (JsonException ex)
			{
				error = $"The script promised JSON, but its output does not parse: {ex.Message}";
				return null;
			}
		}
	}
}
