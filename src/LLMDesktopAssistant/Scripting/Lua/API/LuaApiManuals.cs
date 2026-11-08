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
			--- namespaces([mode]) - global function

			Gets the value containing the available Lua API namespaces.

			Parameters:
			  - mode: string - the return mode (optional):
			    - "string" (default) - returns a string containing the available
			      namespaces, separated by newlines (e.g. "dass\ndass.tool").
			    - "table" - returns a table containing the available namespaces
			      (e.g. { "dass", "dass.tool" }).

			Returns: string or table, depending on the mode parameter.

			--- manuals(namespace) - global function

			Gets the documentation (manuals) for a given Lua API namespace.
			Use this to explore what functions and features are available.

			Parameters:
			  - namespace (one or more): string or table - dot-separated namespace path,
			    e.g. dass, dass.tool, dass.chat, fs, time
			    strings are supported too - "dass", "dass.hook", "random", "web"

			Returns: string - the manuals text for the requested namespace(s).

			Returns an error if the namespace does not exist
			or has no manuals registered.

			Examples:
			  print(namespaces())
			  local namespaces = namespaces("table")
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
			globals["namespaces"] = new LuaCallbackFunction(Namespaces);
			globals["manuals"] = new LuaCallbackFunction(PrintManuals);
			return null;
		}

		private LuaTuple Namespaces(LuaCallingContext ctx, LuaValue[] args)
		{
			bool isTable = false;
			if (args.Length >= 1)
			{
				if (args[0] is not LuaString modeValue)
					throw new LuaRuntimeException("namespaces([mode]): invalid argument type for 'mode' parameter. Expected 'string'.");
				isTable = modeValue.Value == "table";
			}

			var lua = _services.GetRequiredService<LuaService>();

			if (isTable)
			{
				var result = new LuaTable();
				foreach (var ns in lua.Namespaces)
					result.Append(ns ?? LuaVariables.GlobalTable);
				return new LuaTuple(result);
			}
			else
			{
				var result = new StringBuilder();
				foreach (var ns in lua.Namespaces)
					result.AppendLine(ns ?? LuaVariables.GlobalTable);
				return new LuaTuple(new LuaString(result.ToString()));
			}
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
					return new LuaTuple(new LuaString($"Error: invalid namespace argument at index {i + 1}."));
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
