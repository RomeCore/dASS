using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API root namespace <c>dass.*</c>.
	/// Provides application-level utilities: version, help, working directory.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiRoot : LuaApiBase
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private static readonly string _version = App.Version.ToString();

		public override string? Namespace => "dass";

		public override string? Manuals => $"""
			--- dass - application-level utilities

			Provides version info, help system, and working directory.

			FUNCTIONS:

			--- dass.version()
			  Returns the application version string.
			  Returns: string - e.g. "1.0.0"

			--- dass.help()
			  Prints the manuals for all available Lua API namespaces.
			  Returns: nil (output goes to Lua console via print)

			--- dass.working_dir()
			  Returns the current chat's working directory (absolute path).
			  Returns: string - e.g. "{Directories.DefaultWorkingDirectory}"

			EXAMPLES:

			  print(dass.version())        -- "1.0.0"
			  dass.help()                  -- prints all available APIs
			""";

		public LuaApiRoot(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["version"] = new LuaCallbackFunction((ctx, args) =>
				new LuaTuple(new LuaString(_version)));

			ns["help"] = new LuaCallbackFunction((ctx, args) =>
			{
				var printFunc = globals.Get("print");

				foreach (var nsName in luaService.Namespaces)
				{
					var displayName = nsName ?? "_G (global)";
					var nsTable = nsName != null ? luaService.TryResolveNamespace(nsName) : globals;
					if (nsTable == null) continue;

					var manualsValue = nsTable.Get(LuaVariables.NamespaceManuals);
					if (manualsValue is not LuaTable manualsTable) continue;

					if (printFunc is LuaFunction printFn)
					{
						printFn.Invoke(ctx, new LuaString($"\n=== {displayName} ==="));

						foreach (var m in manualsTable.Values)
						{
							if (m is LuaString)
								printFn.Invoke(ctx, m);
						}
					}
				}

				return new LuaTuple(LuaNil.Instance);
			});

			ns["working_dir"] = new LuaCallbackFunction((ctx, args) =>
				new LuaTuple(new LuaString(_fileAccess.GetWorkingDirectory())));

			return null;
		}
	}
}
