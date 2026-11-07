using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for glob searches over the file system: <c>fs.glob</c>. Lives in the <c>fs</c>
	/// namespace because it walks real directories, unlike the pure <c>glob</c> predicates.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiFileSystemGlob(IWorkingDirectoryAccessService fileAccess) : LuaApiBase
	{
		public override string? Namespace => "fs";

		public override string? Manuals => """
			--- fs.glob(pattern, [path], [opts])
			  Finds files and directories by glob pattern.
			  Syntax: *, ?, [...], {...}, **. A leading literal part of the pattern becomes the
			  search root, so an absolute pattern works as well as a relative one.
			  Scanning follows the platform's case rules (case-insensitive on Windows).

			  Parameters:
			    - pattern: string - the glob pattern, e.g. "**/*.cs" or "src/**/*.{md,txt}"
			    - path: string (optional) - the starting directory, default ".", i.e. the
			      working directory. An absolute path is allowed.
			    - opts: table (optional) - options:
			        limit: number - maximum entries, 0 for unlimited (default 100)
			        files: boolean - include files (default true)
			        directories: boolean - include directories (default false)
			        ignore: table - names or single-segment patterns to skip; a matching
			          directory is not descended into. The default set is
			          {".*", "node_modules", "__pycache__", "bin", "obj", "TestResults"} and any
			          table you pass replaces it - an empty table skips nothing and so includes
			          hidden entries and build output.

			  Returns: table - array of entries in file system order. Each entry:
			    - path: string - absolute path, ready for fs.read()
			    - relative: string - path relative to the 'path' argument, so with the default
			      path it is relative to the working directory
			    - name: string - the last segment
			    - is_dir: boolean

			  Errors: an invalid pattern or a 'path' that is missing or not a directory raises a
			  runtime error. No matches is not an error and returns an empty table.

			EXAMPLES:

			  -- Every C# file under the working directory
			  for _, e in ipairs(fs.glob("**/*.cs")) do
			    print(e.relative)
			  end

			  -- One directory, unlimited, including directories
			  local all = fs.glob("*", "docs", { limit = 0, directories = true })

			  -- Search that also walks node_modules and hidden folders
			  local everything = fs.glob("**/*.json", ".", { ignore = {} })
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["glob"] = new LuaCallbackFunction(Glob);
			return null;
		}

		private LuaTuple Glob(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.glob(pattern, [path], [opts]): at least 1 argument expected.");

			var pattern = GetString("fs.glob()", args, 0, "pattern");
			var path = args.Length > 1 && args[1] is not LuaNil
				? GetString("fs.glob()", args, 1, "path")
				: ".";

			var limit = 100;
			var files = true;
			var directories = false;
			var ignore = GlobTextSet.DefaultIgnore;

			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaTable options)
					throw new LuaRuntimeException("fs.glob(): third argument must be a table (options).");

				var limitValue = options.Get("limit");
				if (limitValue is LuaNumber limitNumber)
					limit = (int)limitNumber.Value;
				else if (limitValue is not LuaNil)
					throw new LuaRuntimeException("fs.glob(): 'limit' must be a number.");

				var filesValue = options.Get("files");
				if (filesValue is LuaBoolean filesBoolean)
					files = filesBoolean.Value;
				else if (filesValue is not LuaNil)
					throw new LuaRuntimeException("fs.glob(): 'files' must be a boolean.");

				var directoriesValue = options.Get("directories");
				if (directoriesValue is LuaBoolean directoriesBoolean)
					directories = directoriesBoolean.Value;
				else if (directoriesValue is not LuaNil)
					throw new LuaRuntimeException("fs.glob(): 'directories' must be a boolean.");

				var ignoreValue = options.Get("ignore");
				if (ignoreValue is LuaTable ignoreTable)
					ignore = ParseIgnore(ignoreTable);
				else if (ignoreValue is not LuaNil)
					throw new LuaRuntimeException("fs.glob(): 'ignore' must be a table of strings.");
			}

			var start = fileAccess.GetFullPath(path);

			if (!Directory.Exists(start))
			{
				if (File.Exists(start))
					throw new LuaRuntimeException($"fs.glob(): not a directory: {path}");

				throw new LuaRuntimeException($"fs.glob(): directory does not exist: {path}");
			}

			try
			{
				var search = GlobSearch.From(pattern, start, GlobPathMatcher.DefaultOptions);
				var walkOptions = new GlobWalkOptions
				{
					Files = files,
					Directories = directories,
					Limit = limit,
					Ignore = ignore,
				};

				var result = new LuaTable();
				var index = 1;

				foreach (var entry in GlobWalker.Enumerate(search, walkOptions))
				{
					var item = new LuaTable();
					item["path"] = new LuaString(entry.FullPath);
					item["relative"] = new LuaString(
						Path.GetRelativePath(search.StartingPath, entry.FullPath));
					item["name"] = new LuaString(entry.Name);
					item["is_dir"] = LuaBoolean.FromBoolean(entry.IsDirectory);

					result[index++] = item;
				}

				return new LuaTuple(result);
			}
			catch (GlobException exception)
			{
				throw new LuaRuntimeException(exception.Message);
			}
		}

		private static GlobTextSet ParseIgnore(LuaTable table)
		{
			var patterns = new List<string>(table.Length);

			for (var i = 1; i <= table.Length; i++)
			{
				var value = table.Get(i);

				if (value is LuaString pattern)
					patterns.Add(pattern.Value);
				else if (value is not LuaNil)
					throw new LuaRuntimeException("fs.glob(): 'ignore' must be a table of strings.");
			}

			return GlobTextSet.ParseNames(patterns, GlobCase.PlatformDefault);
		}

		private static string GetString(string function, LuaValue[] args, int index, string name)
		{
			if (args[index] is not LuaString value)
				throw new LuaRuntimeException($"{function}: argument {index + 1} must be a string ({name}).");

			return value.Value;
		}
	}
}
