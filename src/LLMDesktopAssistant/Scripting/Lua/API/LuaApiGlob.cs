using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for glob patterns over text and paths: <c>glob.*</c>. Pure: it never touches the file
	/// system and never resolves the working directory. <c>fs.glob</c> is the one that walks.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiGlob : LuaApiBase
	{
		public override string? Namespace => "glob";

		public override string? Manuals => """
			--- glob - glob pattern matching API

			Matches text and paths against glob patterns. Nothing is read from the file system and
			no path is resolved against the working directory - use fs.glob() for the files themselves.

			Syntax: * (any run of characters), ? (one character), [...] (character class),
			{a,b} (alternatives). A path pattern also understands ** (zero or more whole segments);
			a flat text pattern rejects it.

			FUNCTIONS:

			--- glob.test(pattern, input, [flags])
			  Full-string match of a flat text: a name, an id, a mime type.
			  '*' and '?' cross every character here, separators included.
			  Parameters:
			    - pattern: string - glob pattern
			    - input: string - text to test
			    - flags: string (optional) - "i" ignore case, "t" enable the terminal ':*' operator
			  Returns: boolean

			--- glob.test_path(pattern, path, [flags])
			  Matches a path against a path pattern without walking anything.
			  Both '/' and '\' separate segments; '.' and '..' are folded lexically.
			  Matching starts at the first segment, so a relative pattern matches relative paths
			  and an absolute pattern matches absolute ones.
			  Parameters:
			    - pattern: string - glob pattern, e.g. "src/**/*.cs"
			    - path: string - path to test, e.g. "src/a/b.cs"
			    - flags: string (optional) - "i" ignore case
			  Returns: boolean

			EXAMPLES:

			  print(glob.test("*.md", "README.md"))               -- true
			  print(glob.test("*.MD", "README.md"))               -- false
			  print(glob.test("*.MD", "README.md", "i"))          -- true
			  print(glob.test_path("src/**/*.cs", "src/a/b.cs"))  -- true
			  print(glob.test_path("src/**/*.cs", "lib/a.cs"))    -- false

			  -- A pattern the dialect does not cover raises instead of matching nothing
			  local ok, err = pcall(glob.test, "a**b", "ab")   -- false, "'**' is not supported"
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["test"] = new LuaCallbackFunction(Test);
			ns["test_path"] = new LuaCallbackFunction(TestPath);
			return null;
		}

		private static LuaTuple Test(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("glob.test(pattern, input, [flags]): at least 2 arguments expected.");

			var pattern = GetString("glob.test(pattern, input, [flags])", args, 0, "pattern");
			var input = GetString("glob.test(pattern, input, [flags])", args, 1, "input");
			var options = ParseFlags("glob.test(pattern, input, [flags])", args, 2, allowArgumentTail: true);

			try
			{
				var compiled = GlobTextPattern.Parse(pattern, options);
				return new LuaTuple(LuaBoolean.FromBoolean(compiled.Matches(input)));
			}
			catch (GlobException exception)
			{
				throw new LuaRuntimeException(exception.Message);
			}
		}

		private static LuaTuple TestPath(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("glob.test_path(pattern, path, [flags]): at least 2 arguments expected.");

			var pattern = GetString("glob.test_path(pattern, path, [flags])", args, 0, "pattern");
			var path = GetString("glob.test_path(pattern, path, [flags])", args, 1, "path");
			var options = ParseFlags("glob.test_path(pattern, path, [flags])", args, 2, allowArgumentTail: false);

			try
			{
				var compiled = GlobPattern.Parse(pattern, options);
				return new LuaTuple(LuaBoolean.FromBoolean(GlobPathMatcher.Matches(compiled, path)));
			}
			catch (GlobException exception)
			{
				throw new LuaRuntimeException(exception.Message);
			}
		}

		private static string GetString(string function, LuaValue[] args, int index, string name)
		{
			if (args[index] is not LuaString value)
				throw new LuaRuntimeException($"{function}: argument {index + 1} must be a string ({name}).");

			return value.Value;
		}

		private static GlobOptions ParseFlags(string function, LuaValue[] args, int index,
			bool allowArgumentTail)
		{
			if (args.Length <= index || args[index] is LuaNil)
				return GlobOptions.None;

			if (args[index] is not LuaString flags)
				throw new LuaRuntimeException($"{function}: flags must be a string.");

			var options = GlobOptions.None;

			foreach (var flag in flags.Value)
			{
				options |= flag switch
				{
					'i' => GlobOptions.IgnoreCase,
					't' when allowArgumentTail => GlobOptions.ArgumentTail,
					_ => throw new LuaRuntimeException($"{function}: unknown flag '{flag}'."),
				};
			}

			return options;
		}
	}
}
