using System.Text;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiManuals : LuaApiBase
	{
		public override string? Namespace => null;

		public override string? Manuals => """
			--- namespaces() - global function

			Gets the string containing the list of available Lua API namespaces.

			Parameters:
			  - None

			Returns: string - the list of available namespaces, separated by newlines.

			--- manuals(namespace) - global function

			Gets the documentation (manuals) for a given Lua API namespace.
			Use this to explore what functions and features are available.

			Parameters:
			  - namespace (one or more): string or table - dot-separated namespace path,
			    e.g. dass, dass.tool, dass.chat, fs, time

			Returns: string - the manuals text for the requested namespace(s).

			Returns an error if the namespace does not exist
			or has no manuals registered.

			Examples:
			  print(namespaces())
			  print(manuals(dass))
			  print(manuals(dass.tool, dass.tool.result, fs, time))
			""";

		private readonly IServiceProvider _services;

		public LuaApiManuals(IServiceProvider services)
		{
			_services = services;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			globals["namespaces"] = new LuaCallbackFunction(PrintNamespaces);
			globals["manuals"] = new LuaCallbackFunction(PrintManuals);
			return null;
		}

		private LuaTuple PrintNamespaces(LuaCallingContext ctx, LuaValue[] args)
		{
			var lua = _services.GetRequiredService<LuaService>();
			var result = new StringBuilder();

			foreach (var ns in lua.Namespaces)
			{
				result.AppendLine(ns ?? LuaVariables.GlobalTable);
			}

			return new LuaTuple(new LuaString(result.ToString()));
		}

		private LuaTuple PrintManuals(LuaCallingContext ctx, LuaValue[] args)
		{
			var lua = _services.GetRequiredService<LuaService>();
			var result = new StringBuilder();

			for (int i = 0; i < args.Length; i++)
			{
				var nsArg = args[i];
				LuaTable? nsTable;

				if (nsArg is LuaString str)
				{
					nsTable = lua.TryResolveNamespace(str.Value);
					if (nsTable == null)
						return new LuaTuple(new LuaString($"Error: namespace '{str.Value}' not found."));
				}
				else if (nsArg is LuaTable table)
				{
					nsTable = table;
				}
				else
				{
					nsTable = ctx.Globals;
				}

				// Look for a "_manuals" subtable inside the resolved namespace
				var manuals = nsTable.Get(LuaVariables.NamespaceManuals);
				var nsPath = nsTable.Get(LuaVariables.NamespaceFullPath).ToString();

				if (manuals is not LuaTable manTable || manTable.Length == 0)
				{
					result.AppendLine($"No manuals found for namespace '{nsPath}'.");
				}
				else
				{
					result.AppendLine($"--- Manuals for namespace '{nsPath}' ---");
					result.AppendLine();

					foreach (var entry in manTable.Values)
					{
						if (entry is LuaString manStr)
							result.AppendLine(manStr.Value.TrimEnd()).AppendLine();
					}
				}
			}

			return new LuaTuple(new LuaString(result.ToString().TrimEnd()));
		}
	}
}
