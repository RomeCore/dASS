using System.Text.RegularExpressions;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for regular expressions: <c>regex.*</c>.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiRegex : LuaApiBase
	{
		public override string? Namespace => "regex";

		public override string? Manuals => """
			--- regex - regular expressions API

			Provides classic regular expression operations using .NET regex syntax.
			All functions accept an optional 'flags' string parameter.

			Available flags (combine as needed, e.g. "im"):
			  i - ignore case
			  m - multiline (^ and $ match line boundaries)
			  s - singleline (. matches newline)
			  x - ignore pattern whitespace
			  c - (replace only) replace only the first occurrence

			FUNCTIONS:

			--- regex.test(pattern, text, [flags])
			  Returns true if the pattern matches anywhere in the text.
			  Parameters:
			    - pattern: string - Regular expression
			    - text: string - Text to search
			    - flags: string (optional) - Regex flags
			  Returns: boolean

			--- regex.match(pattern, text, [flags])
			  Returns the first match with detailed info.
			  Parameters:
			    - pattern: string - Regular expression
			    - text: string - Text to search
			    - flags: string (optional) - Regex flags
			  Returns: table or nil
			  Result fields:
			    - value: string - The matched text
			    - start: number - Start position (1-based)
			    - end: number - End position (1-based, inclusive)
			    - length: number - Match length
			    - groups: table - Group captures. Numeric keys (1..n) and named keys.
			      Each group: { value (string or nil), start, end, length }
				- group_names: array of strings - Named group names

			  NOTE: `end` is a Lua reserved word, so that field MUST be accessed with
			  brackets: `m["end"]` - writing `m.end` is a syntax error.
			  Same applies to group tables: `m.groups[1]["end"]`.


			--- regex.matches(pattern, text, [flags])
			  Returns all non-overlapping matches.
			  Parameters:
			    - pattern: string - Regular expression
			    - text: string - Text to search
			    - flags: string (optional) - Regex flags
			  Returns: array of match tables (same as regex.match result) - NOT an array of strings!
			Iterate as: for _, m in ipairs(regex.matches(...)) do print(m.value) end

			--- regex.replace(pattern, replacement, text, [flags])
			  Replaces matches with replacement string.
			  Parameters:
			    - pattern: string - Regular expression
			    - replacement: string - Replacement (supports $1, ${name}, $&, $`, $', $_)
			    - text: string - Input text
			    - flags: string (optional) - Regex flags, plus "c" for count=1
			  Returns: string

			--- regex.split(pattern, text, [flags])
			  Splits text by the regex pattern.
			  Parameters:
			    - pattern: string - Regular expression delimiter
			    - text: string - Text to split
			    - flags: string (optional) - Regex flags
			  Returns: array of strings

			--- regex.escape(text)
			  Escapes all regex metacharacters in the text.
			  Parameters:
			    - text: string - Text to escape
			  Returns: string

			EXAMPLES:

			  -- Test
			  if regex.test("hello", "Hello World", "i") then
			    print("Found!")
			  end

			  -- Match with groups
			  local m = regex.match("(\\w+)@(\\w+)", "user@example.com")
			  print(m.groups[1].value) -- "user"

			  -- Position info: 'end' is a reserved word -> use brackets!
			  local m2 = regex.match("\\d+", "abc123def")
			  print(m2.value, m2.start, m2["end"], m2.length) -- 123  4  6  3

			  -- All matches
			  for _, w in ipairs(regex.matches("\\w+", "Hello World")) do
			    print(w.value)
			  end

			  -- Replace
			  print(regex.replace("\\d+", "[#]", "abc123def456")) -- "abc[#]def[#]"

			  -- Split
			  local parts = regex.split("[,\\s]+", "a, b, c")
			  print(parts[1]) -- "a"

			  -- Escape
			  print(regex.escape("(hello)")) -- "\\(hello\\)"
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["test"] = new LuaCallbackFunction(Test);
			ns["match"] = new LuaCallbackFunction(Match);
			ns["matches"] = new LuaCallbackFunction(Matches);
			ns["replace"] = new LuaCallbackFunction(Replace);
			ns["split"] = new LuaCallbackFunction(Split);
			ns["escape"] = new LuaCallbackFunction(Escape);
			return null;
		}

		private static LuaTuple Test(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("regex.test(pattern, text, [flags]): at least 2 arguments expected.");

			if (args[0] is not LuaString patternVal)
				throw new LuaRuntimeException("regex.test(): first argument must be a string (pattern).");
			if (args[1] is not LuaString textVal)
				throw new LuaRuntimeException("regex.test(): second argument must be a string (text).");

			var flags = ParseFlags(args, 2);
			try
			{
				var regex = new Regex(patternVal.Value, flags);
				return new LuaTuple(LuaBoolean.FromBoolean(regex.IsMatch(textVal.Value)));
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static LuaTuple Match(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("regex.match(pattern, text, [flags]): at least 2 arguments expected.");

			if (args[0] is not LuaString patternVal)
				throw new LuaRuntimeException("regex.match(): first argument must be a string (pattern).");
			if (args[1] is not LuaString textVal)
				throw new LuaRuntimeException("regex.match(): second argument must be a string (text).");

			var flags = ParseFlags(args, 2);
			try
			{
				var regex = new Regex(patternVal.Value, flags);
				var match = regex.Match(textVal.Value);
				if (!match.Success)
					return new LuaTuple(LuaNil.Instance);

				return new LuaTuple(MatchToTable(regex, match));
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static LuaTuple Matches(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("regex.matches(pattern, text, [flags]): at least 2 arguments expected.");

			if (args[0] is not LuaString patternVal)
				throw new LuaRuntimeException("regex.matches(): first argument must be a string (pattern).");
			if (args[1] is not LuaString textVal)
				throw new LuaRuntimeException("regex.matches(): second argument must be a string (text).");

			var flags = ParseFlags(args, 2);
			try
			{
				var regex = new Regex(patternVal.Value, flags);
				var matches = regex.Matches(textVal.Value);

				var resultArray = new LuaTable();
				for (int i = 0; i < matches.Count; i++)
				{
					resultArray[i + 1] = MatchToTable(regex, matches[i]);
				}
				return new LuaTuple(resultArray);
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static LuaTuple Replace(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
				throw new LuaRuntimeException("regex.replace(pattern, replacement, text, [flags]): at least 3 arguments expected.");

			if (args[0] is not LuaString patternVal)
				throw new LuaRuntimeException("regex.replace(): first argument must be a string (pattern).");
			if (args[1] is not LuaString replacementVal)
				throw new LuaRuntimeException("regex.replace(): second argument must be a string (replacement).");
			if (args[2] is not LuaString textVal)
				throw new LuaRuntimeException("regex.replace(): third argument must be a string (text).");

			var (regexOptions, countOne) = ParseReplaceFlags(args, 3);

			try
			{
				var regex = new Regex(patternVal.Value, regexOptions);

				string result;
				if (countOne)
					result = regex.Replace(textVal.Value, replacementVal.Value, 1);
				else
					result = regex.Replace(textVal.Value, replacementVal.Value);

				return new LuaTuple(new LuaString(result));
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static LuaTuple Split(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("regex.split(pattern, text, [flags]): at least 2 arguments expected.");

			if (args[0] is not LuaString patternVal)
				throw new LuaRuntimeException("regex.split(): first argument must be a string (pattern).");
			if (args[1] is not LuaString textVal)
				throw new LuaRuntimeException("regex.split(): second argument must be a string (text).");

			var flags = ParseFlags(args, 2);
			try
			{
				var regex = new Regex(patternVal.Value, flags);
				var parts = regex.Split(textVal.Value);

				var resultArray = new LuaTable();
				for (int i = 0; i < parts.Length; i++)
				{
					resultArray[i + 1] = new LuaString(parts[i]);
				}
				return new LuaTuple(resultArray);
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static LuaTuple Escape(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("regex.escape(text): at least 1 argument expected.");

			if (args[0] is not LuaString textVal)
				throw new LuaRuntimeException("regex.escape(): first argument must be a string (text).");

			try
			{
				return new LuaTuple(new LuaString(Regex.Escape(textVal.Value)));
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"Regex error: {ex.Message}");
			}
		}

		private static RegexOptions ParseFlags(LuaValue[] args, int flagsIndex)
		{
			var options = RegexOptions.None;

			if (args.Length > flagsIndex && args[flagsIndex] is LuaString flagsStr)
			{
				foreach (char c in flagsStr.Value)
				{
					switch (c)
					{
						case 'i': options |= RegexOptions.IgnoreCase; break;
						case 'm': options |= RegexOptions.Multiline; break;
						case 's': options |= RegexOptions.Singleline; break;
						case 'x': options |= RegexOptions.IgnorePatternWhitespace; break;
						case 'c': break;
						default:
							throw new LuaRuntimeException($"Unknown regex flag: '{c}'. Supported: i, m, s, x, c");
					}
				}
			}

			return options;
		}

		private static (RegexOptions Options, bool CountOne) ParseReplaceFlags(LuaValue[] args, int flagsIndex)
		{
			var options = RegexOptions.None;
			bool countOne = false;

			if (args.Length > flagsIndex && args[flagsIndex] is LuaString flagsStr)
			{
				foreach (char c in flagsStr.Value)
				{
					switch (c)
					{
						case 'i': options |= RegexOptions.IgnoreCase; break;
						case 'm': options |= RegexOptions.Multiline; break;
						case 's': options |= RegexOptions.Singleline; break;
						case 'x': options |= RegexOptions.IgnorePatternWhitespace; break;
						case 'c': countOne = true; break;
						default:
							throw new LuaRuntimeException($"Unknown regex flag: '{c}'. Supported: i, m, s, x, c");
					}
				}
			}

			return (options, countOne);
		}

		private static LuaTable MatchToTable(Regex regex, Match match)
		{
			var t = new LuaTable();

			t["value"] = new LuaString(match.Value);
			t["start"] = new LuaNumber(match.Index + 1);
			t["end"] = new LuaNumber(match.Index + match.Length);
			t["length"] = new LuaNumber(match.Length);

			// Group names list
			var groupNamesTable = new LuaTable();
			var groupNames = regex.GetGroupNames();
			int nameIdx = 1;
			foreach (var name in groupNames)
			{
				if (!int.TryParse(name, out _))
				{
					groupNamesTable[nameIdx] = new LuaString(name);
					nameIdx++;
				}
			}
			t["group_names"] = nameIdx == 1 ? new LuaTable() : groupNamesTable;

			// Groups
			var groupsTable = new LuaTable();
			foreach (var name in groupNames)
			{
				var group = match.Groups[name];
				var groupTable = new LuaTable();

				if (group.Success)
				{
					groupTable["value"] = new LuaString(group.Value);
					groupTable["start"] = new LuaNumber(group.Index + 1);
					groupTable["end"] = new LuaNumber(group.Index + group.Length);
					groupTable["length"] = new LuaNumber(group.Length);
				}
				else
				{
					groupTable["value"] = LuaNil.Instance;
					groupTable["start"] = LuaNil.Instance;
					groupTable["end"] = LuaNil.Instance;
					groupTable["length"] = new LuaNumber(0);
				}

				if (int.TryParse(name, out int numericKey))
					groupsTable[numericKey] = groupTable;
				else
					groupsTable[name] = groupTable;
			}
			t["groups"] = groupsTable;

			return t;
		}
	}
}
