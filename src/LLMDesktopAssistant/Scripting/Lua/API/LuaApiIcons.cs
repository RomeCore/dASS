using AsyncLua;
using AsyncLua.Values;
using Material.Icons;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for Material Design icons: <c>icon.*</c>.
	/// Provides access to MaterialIconKind enum for status icons, toasts, etc.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiIcons : LuaApiBase
	{
		public override string? Namespace => "icon";

		public override string? Manuals => """
			--- icon — Material Design icons API

			Provides access to Material Design icons (MaterialIconKind enum).
			Useful for setting status icons in tool results, toasts, etc.

			FUNCTIONS:

			--- icon.exists(kind)
			  Checks if an icon with the given name exists.
			  Parameters:
			    - kind: string — icon name (case-insensitive, e.g. "File", "Web", "Alert")
			  Returns: boolean

			--- icon.get(kind)
			  Gets detailed information about an icon by its name.
			  Parameters:
			    - kind: string — icon name (case-insensitive)
			  Returns: table or nil — with fields:
			    - name: string — canonical icon name
			    - kind: number — numeric enum value

			--- icon.list([filter])
			  Lists all available icon names.
			  Parameters:
			    - filter: string (optional) — substring filter (case-insensitive)
			  Returns: table — array of icon name strings (sorted alphabetically)

			--- icon.random()
			  Returns a random icon name.
			  Returns: string — random icon name

			EXAMPLES:

			  if icon.exists("Alert") then
			    print("Alert icon exists!")
			  end

			  local info = icon.get("File")
			  print(info.name, info.kind)

			  -- Filter icons
			  local fileIcons = icon.list("file")
			  for _, name in ipairs(fileIcons) do
			    print(name)
			  end

			  -- Use with tool result
			  dass.tool_result.set_status(icon.get("Download").name, "Downloading...")

			NOTES:
			  - All lookups are case-insensitive.
			  - The list is cached at startup for performance.
			  - There are over 7000 icons available.
			""";

		private static readonly Dictionary<string, MaterialIconKind> _iconMap;
		private static readonly string[] _allIconNames;

		static LuaApiIcons()
		{
			// Enumerate field names rather than values: several members share a value (aliases, e.g.
			// "Bulb" == "Idea"), and Enum.GetValues()/ToString() only ever yields the first name.
			_iconMap = new Dictionary<string, MaterialIconKind>(StringComparer.OrdinalIgnoreCase);
			foreach (var name in Enum.GetNames<MaterialIconKind>())
			{
				if (Enum.TryParse<MaterialIconKind>(name, out var kind))
					_iconMap.TryAdd(name, kind);
			}

			_allIconNames = _iconMap.Keys.ToArray();

			Array.Sort(_allIconNames, StringComparer.OrdinalIgnoreCase);
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["exists"] = new LuaCallbackFunction(Exists);
			ns["get"] = new LuaCallbackFunction(Get);
			ns["list"] = new LuaCallbackFunction(List);
			ns["random"] = new LuaCallbackFunction(Random);
			return null;
		}

		private LuaTuple Exists(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("icon.exists(kind): at least 1 argument expected.");

			args[0].TryToString(out var kind);
			if (string.IsNullOrEmpty(kind))
				return new LuaTuple(LuaBoolean.False);

			return new LuaTuple(LuaBoolean.FromBoolean(_iconMap.ContainsKey(kind)));
		}

		private LuaTuple Get(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("icon.get(kind): at least 1 argument expected.");

			args[0].TryToString(out var kind);
			if (string.IsNullOrEmpty(kind))
				return new LuaTuple(LuaNil.Instance);

			if (!_iconMap.TryGetValue(kind, out var iconKind))
				return new LuaTuple(LuaNil.Instance);

			var table = new LuaTable();
			table.Set("name", iconKind.ToString());
			table.Set("kind", (int)iconKind);
			return new LuaTuple(table);
		}

		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = new LuaTable();

			if (args.Length == 0)
			{
				foreach (var name in _allIconNames)
				{
					result.Append(new LuaString(name));
				}
			}
			else
			{
				args[0].TryToString(out var filter);
				foreach (var name in _allIconNames)
				{
					if (name.Contains(filter, StringComparison.OrdinalIgnoreCase))
					{
						result.Append(new LuaString(name));
					}
				}
			}

			return new LuaTuple(result);
		}

		private LuaTuple Random(LuaCallingContext ctx, LuaValue[] args)
		{
			var index = System.Random.Shared.Next(_allIconNames.Length);
			return new LuaTuple(new LuaString(_allIconNames[index]));
		}
	}
}
