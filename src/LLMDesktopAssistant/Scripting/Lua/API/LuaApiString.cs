using System.Globalization;
using System.Text;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API with extended string utilities: <c>string.*</c>.
	/// Supplements the built-in string library with commonly needed functions.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiString : LuaApiBase
	{
		public override string? Namespace => "string";

		public override string? Manuals => """
			--- string - extended string utilities

			Supplements the built-in Lua string library.

			FUNCTIONS:

			--- string.split(str, delimiter)
			  Splits a string by a delimiter.
			  Parameters:
			    - str: string - string to split
			    - delimiter: string or pattern (optional, default "%s+") - delimiter pattern
			  Returns: table - array of substrings

			--- string.trim(str)
			--- string.trim_left(str)
			--- string.trim_right(str)
			  Removes whitespace from both ends (or one side) of a string.

			--- string.startswith(str, prefix)
			--- string.endswith(str, suffix)
			  Checks if a string starts/ends with a given substring.
			  Returns: boolean

			--- string.contains(str, substr)
			  Checks if a string contains a substring.
			  Returns: boolean

			--- string.slug(str)
			  Converts a string to a URL-friendly slug.
			  Replaces spaces with hyphens, removes special chars,
			  converts to lowercase, handles basic transliteration.
			  Parameters:
			    - str: string - input text
			  Returns: string - e.g. "Hello World!" -> "hello-world"

			--- string.wrap(str, width, [indent])
			  Wraps text to a specified line width.
			  Parameters:
			    - str: string - text to wrap
			    - width: number - maximum line width (default: 80)
			    - indent: string (optional) - prefix for each line (e.g. "> ")
			  Returns: string - wrapped text

			--- string.truncate(str, max_len, [suffix])
			  Truncates a string to a maximum length, appending a suffix if trimmed.
			  Parameters:
			    - str: string - input text
			    - max_len: number - maximum length
			    - suffix: string (optional, default "...") - suffix to append
			  Returns: string

			--- string.pad_left(str, total_width, [char])
			--- string.pad_right(str, total_width, [char])
			  Pads a string to a given width with a character.
			  Parameters:
			    - str: string - input string
			    - total_width: number - target length
			    - char: string (optional, default " ") - padding character
			  Returns: string

			--- string.lines(str)
			  Splits a string into lines (handles \\n, \\r\\n, \\r).
			  Returns: table - array of strings

			--- string.upper_first(str)
			  Capitalizes the first character of a string.
			  Parameters:
			    - str: string - input text
			  Returns: string - e.g. "hello" -> "Hello"

			EXAMPLES:

			  local parts = string.split("a,b,c", ",")
			  print(string.trim("  hello  "))
			  if string.startswith("hello", "he") then end
			  print(string.slug("Hello World!")) -- "hello-world"
			  print(string.wrap("long text...", 40))
			  print(string.truncate("long text", 6)) -- "long t..."
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["split"] = new LuaCallbackFunction(Split);
			ns["trim"] = new LuaCallbackFunction(Trim);
			ns["trim_left"] = new LuaCallbackFunction(TrimLeft);
			ns["trim_right"] = new LuaCallbackFunction(TrimRight);
			ns["startswith"] = new LuaCallbackFunction(StartsWith);
			ns["endswith"] = new LuaCallbackFunction(EndsWith);
			ns["contains"] = new LuaCallbackFunction(Contains);
			ns["slug"] = new LuaCallbackFunction(Slug);
			ns["wrap"] = new LuaCallbackFunction(Wrap);
			ns["truncate"] = new LuaCallbackFunction(Truncate);
			ns["pad_left"] = new LuaCallbackFunction(PadLeft);
			ns["pad_right"] = new LuaCallbackFunction(PadRight);
			ns["lines"] = new LuaCallbackFunction(Lines);
			ns["upper_first"] = new LuaCallbackFunction(UpperFirst);
			return null;
		}

		private static LuaTuple Split(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.split(str, [delimiter]): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.split(): first argument must be a string.");

			string delimiter = "%s+";
			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is not LuaString dVal)
					throw new LuaRuntimeException("string.split(): delimiter must be a string.");
				delimiter = dVal.Value;
			}

			// Escape Lua magic characters for literal splitting,
			// but allow Lua patterns if explicitly using % patterns
			string pattern;
			if (delimiter.Contains('%'))
				pattern = delimiter;
			else
				pattern = EscapeLuaPattern(delimiter);

			var result = new LuaTable();
			var parts = SplitByPattern(strVal.Value, pattern);
			for (int i = 0; i < parts.Length; i++)
				result[i + 1] = new LuaString(parts[i]);

			return new LuaTuple(result);
		}

		private static LuaTuple Trim(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.trim(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.trim(): first argument must be a string.");
			return new LuaTuple(new LuaString(strVal.Value.Trim()));
		}

		private static LuaTuple TrimLeft(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.trim_left(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.trim_left(): first argument must be a string.");
			return new LuaTuple(new LuaString(strVal.Value.TrimStart()));
		}

		private static LuaTuple TrimRight(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.trim_right(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.trim_right(): first argument must be a string.");
			return new LuaTuple(new LuaString(strVal.Value.TrimEnd()));
		}

		private static LuaTuple StartsWith(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.startswith(str, prefix): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaString prefixVal)
				throw new LuaRuntimeException("string.startswith(): both arguments must be strings.");
			return new LuaTuple(LuaBoolean.FromBoolean(strVal.Value.StartsWith(prefixVal.Value)));
		}

		private static LuaTuple EndsWith(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.endswith(str, suffix): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaString suffixVal)
				throw new LuaRuntimeException("string.endswith(): both arguments must be strings.");
			return new LuaTuple(LuaBoolean.FromBoolean(strVal.Value.EndsWith(suffixVal.Value)));
		}

		private static LuaTuple Contains(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.contains(str, substr): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaString substrVal)
				throw new LuaRuntimeException("string.contains(): both arguments must be strings.");
			return new LuaTuple(LuaBoolean.FromBoolean(strVal.Value.Contains(substrVal.Value)));
		}

		private static LuaTuple Slug(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.slug(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.slug(): first argument must be a string.");

			var slug = strVal.Value.ToLowerInvariant();
			slug = RemoveDiacritics(slug);
			var sb = new StringBuilder();
			foreach (char c in slug)
			{
				if (char.IsLetterOrDigit(c))
					sb.Append(c);
				else if (c == ' ' || c == '-' || c == '_' || c == '.' || c == '~')
					sb.Append('-');
			}
			var result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-");
			result = result.Trim('-');
			return new LuaTuple(new LuaString(result));
		}

		private static LuaTuple Wrap(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.wrap(str, [width], [indent]): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.wrap(): first argument must be a string.");

			int width = 80;
			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is not LuaNumber wVal)
					throw new LuaRuntimeException("string.wrap(): width must be a number.");
				width = (int)wVal.Value;
				if (width < 1)
					throw new LuaRuntimeException("string.wrap(): width must be positive.");
			}

			string indent = "";
			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaString indVal)
					throw new LuaRuntimeException("string.wrap(): indent must be a string.");
				indent = indVal.Value;
			}

			var words = strVal.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var result = new StringBuilder();
			int lineLen = indent.Length;

			foreach (var word in words)
			{
				if (lineLen + word.Length > width && lineLen > indent.Length)
				{
					result.AppendLine();
					result.Append(indent);
					lineLen = indent.Length;
				}
				else if (lineLen > indent.Length)
				{
					result.Append(' ');
					lineLen++;
				}

				result.Append(word);
				lineLen += word.Length;
			}

			return new LuaTuple(new LuaString(result.ToString()));
		}

		private static LuaTuple Truncate(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.truncate(str, max_len, [suffix]): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaNumber maxLenVal)
				throw new LuaRuntimeException("string.truncate(): first two arguments must be string and number.");
			int maxLen = (int)maxLenVal.Value;

			string suffix = "...";
			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaString sVal)
					throw new LuaRuntimeException("string.truncate(): suffix must be a string.");
				suffix = sVal.Value;
			}

			if (strVal.Value.Length <= maxLen)
				return new LuaTuple(new LuaString(strVal.Value));

			if (maxLen <= suffix.Length)
				return new LuaTuple(new LuaString(strVal.Value.Substring(0, maxLen)));

			return new LuaTuple(new LuaString(strVal.Value.Substring(0, maxLen - suffix.Length) + suffix));
		}

		private static LuaTuple PadLeft(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.pad_left(str, total_width, [char]): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaNumber widthVal)
				throw new LuaRuntimeException("string.pad_left(): first two arguments must be string and number.");
			int width = (int)widthVal.Value;

			char padChar = ' ';
			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaString pcVal || string.IsNullOrEmpty(pcVal.Value))
					throw new LuaRuntimeException("string.pad_left(): pad char must be a non-empty string.");
				padChar = pcVal.Value[0];
			}

			return new LuaTuple(new LuaString(strVal.Value.PadLeft(width, padChar)));
		}

		private static LuaTuple PadRight(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("string.pad_right(str, total_width, [char]): at least 2 arguments expected.");
			if (args[0] is not LuaString strVal || args[1] is not LuaNumber widthVal)
				throw new LuaRuntimeException("string.pad_right(): first two arguments must be string and number.");
			int width = (int)widthVal.Value;

			char padChar = ' ';
			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaString pcVal || string.IsNullOrEmpty(pcVal.Value))
					throw new LuaRuntimeException("string.pad_right(): pad char must be a non-empty string.");
				padChar = pcVal.Value[0];
			}

			return new LuaTuple(new LuaString(strVal.Value.PadRight(width, padChar)));
		}

		private static LuaTuple Lines(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.lines(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.lines(): first argument must be a string.");

			var lines = strVal.Value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			var result = new LuaTable();
			for (int i = 0; i < lines.Length; i++)
				result[i + 1] = new LuaString(lines[i]);
			return new LuaTuple(result);
		}

		private static LuaTuple UpperFirst(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("string.upper_first(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("string.upper_first(): first argument must be a string.");

			if (string.IsNullOrEmpty(strVal.Value))
				return new LuaTuple(new LuaString(strVal.Value));

			return new LuaTuple(new LuaString(char.ToUpperInvariant(strVal.Value[0]) + strVal.Value.Substring(1)));
		}

		// --- Helpers ---

		private static string EscapeLuaPattern(string s)
		{
			return s.Replace("\\", "\\\\")
					.Replace("^", "\\^")
					.Replace("$", "\\$")
					.Replace("(", "\\(")
					.Replace(")", "\\)")
					.Replace("%", "\\%")
					.Replace(".", "\\.")
					.Replace("[", "\\[")
					.Replace("]", "\\]")
					.Replace("*", "\\*")
					.Replace("+", "\\+")
					.Replace("-", "\\-")
					.Replace("?", "\\?");
		}

		private static string EscapeLuaString(string s)
		{
			return "\"" + s.Replace("\\", "\\\\")
						   .Replace("\"", "\\\"")
						   .Replace("\n", "\\n")
						   .Replace("\r", "\\r")
						   .Replace("\t", "\\t") + "\"";
		}

		private static string[] SplitByPattern(string str, string pattern)
		{
			if (pattern.StartsWith("\\"))
			{
				var unescaped = pattern.Replace("\\", "");
				if (string.IsNullOrEmpty(unescaped))
					return [str];
				return str.Split(new[] { unescaped }, StringSplitOptions.None);
			}

			if (pattern == "%s+")
				return str.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

			return str.Split(new[] { pattern }, StringSplitOptions.None);
		}

		private static string RemoveDiacritics(string text)
		{
			var formD = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder();
			foreach (char c in formD)
			{
				var uc = CharUnicodeInfo.GetUnicodeCategory(c);
				if (uc != UnicodeCategory.NonSpacingMark)
					sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
	}
}
