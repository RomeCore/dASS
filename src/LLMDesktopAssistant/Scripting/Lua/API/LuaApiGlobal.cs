using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiGlobal : LuaApiBase
	{
		public override string? Namespace => null; // Global

		public override string? Manuals => """
			--- _G - global API
			
			Provides additional functions to the global namespace.
			
			FUNCTIONS:
			
			--- dofile(path) -> any
			  Executes a text file as AsyncLua synchronous code and returns the result.
			  Path is resolved relative to the current working directory.
			  Throws exception if file is not found, code cannot be compiled, or runtime error occurs.

			--- async dofile_async(path) -> any
			  Executes a text file as AsyncLua asynchronous code and returns the result as a task.
			  Same as dofile but returns a task that required to be awaited.

			EXAMPLES:

			local result = dofile("example.lua")
			local result = await dofile_async("example_async.lua")
			local ok, result = pcall_async(dofile_async, "example_async.lua")
			""";

		private readonly IWorkingDirectoryAccessService _fileAccess;

		public LuaApiGlobal(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["dofile"] = new LuaCallbackFunction(DoFile);
			ns["dofile_async"] = new LuaCallbackFunction(DoFileAsync);

			return null;
		}

		private LuaTuple DoFile(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dofile(path): at least 1 argument expected.");
			if (args[0] is not LuaString path)
				throw new LuaRuntimeException("dofile(path): argument 1 must be a string.");

			var fullPath = _fileAccess.GetFullPath(path.Value);
			if (!File.Exists(fullPath))
				throw new LuaRuntimeException($"dofile(path): file not found: {fullPath}.");

			var code = File.ReadAllText(fullPath);
			return ctx.Execute(code, fullPath);
		}

		private async Task<LuaTuple> DoFileAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dofile_async(path): at least 1 argument expected.");
			if (args[0] is not LuaString path)
				throw new LuaRuntimeException("dofile_async(path): argument 1 must be a string.");

			var fullPath = _fileAccess.GetFullPath(path.Value);
			if (!File.Exists(fullPath))
				throw new LuaRuntimeException($"dofile_async(path): file not found: {fullPath}.");

			var code = File.ReadAllText(fullPath);
			return await ctx.ExecuteAsync(code, fullPath);
		}
	}
}
