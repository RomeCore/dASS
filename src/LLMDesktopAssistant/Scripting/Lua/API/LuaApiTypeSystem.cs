using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for type metatable manipulation.
	/// Provides global functions <c>gettypemetatable</c>, <c>settypemetatable</c>,
	/// and <c>extendtypemetatable</c> for working with type metatables
	/// on primitive Lua types.
	/// 
	/// Type metatables let you override default behavior of ALL values of a given type,
	/// similar to how regular metatables work on tables.
	/// 
	/// Supported type names: "nil", "boolean", "number", "string", "function"
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiTypeSystem : LuaApiBase
	{
		public override string? Namespace => null;

		public override string? Manuals => """
			--- gettypemetatable(type) - get type metatable for a primitive Lua type
			
			Returns the current metatable for ALL values of a primitive Lua type.
			
			Parameters:
			  - type: string - type name (same as settypemetatable).
			
			Returns: table or nil - the current type metatable.
			
			--- settypemetatable(type, metatable) - set type metatable for a primitive Lua type

			Sets the metatable for ALL values of a primitive Lua type.
			This allows overriding default behaviour for
			numbers, strings, booleans, functions, nil, and void.

			Parameters:
			  - type: string - type name:
			    "nil", "boolean", "number", "string", "function"
			    (also accepts short aliases: "bool", "num", "str", "func")
			  - metatable: table or nil - the metatable to set, or nil to clear.

			Returns: nil

			Throws if the type is not recognized.

			--- extendtypemetatable(type, extensions) - merge fields into the current type metatable

			Merges the provided table into the existing type metatable for the given type,
			rather than replacing it entirely. Useful for adding individual metamethods
			without overwriting ones already set.

			Parameters:
			  - type: string - type name (same as settypemetatable).
			  - extensions: table - table whose key-value pairs will be merged into
			    the type metatable. Overwrites existing keys if they match.

			Returns: nil

			Throws if the type is not recognized.

			EXAMPLES:

			  -- 1. Custom number formatting (use tostring(), not print())
			  settypemetatable("number", {
			    __tostring = function(n)
			      return string.format("%.2f", n)
			    end
			  })
			  print(tostring(3.14159)) -- "3.14"

			  -- 2. Using gettypemetatable
			  local mt = gettypemetatable("number")
			  print(mt) -- the number metatable

			  -- 3. Adding methods to numbers via __index (colon syntax!)
			  settypemetatable("number", {
			    __index = {
			      double = function(self) return self * 2 end,
			      is_even = function(self) return self % 2 == 0 end,
			      hex = function(self) return string.format("%X", self) end
			    }
			  })
			  print((5):double())    -- 10  (use colon for method call)
			  print((7):is_even())    -- false
			  print((255):hex())      -- "FF"

			  -- 4. Boolean __tostring (via tostring)
			  settypemetatable("boolean", {
			    __tostring = function(b)
			      return b and "✓ Yes" or "✗ No"
			    end
			  })
			  print(tostring(true))  -- "✓ Yes"
			  print(tostring(false)) -- "✗ No"

			  -- 5. Custom __len for numbers
			  settypemetatable("number", {
			    __len = function(n)
			      return n % 10
			    end
			  })
			  print(#42)  -- 2  (42 % 10 = 2)

			  -- 6. Custom pairs iteration over numbers
			  settypemetatable("number", {
			    __pairs = function(n)
			      local i = 0
			      return function()
			        i = i + 1
			        if i <= n then return i, i ^ 2 end
			      end
			    end
			  })
			  for k, v in pairs(5) do print(k, v) end
			  -- 1  1
			  -- 2  4
			  -- 3  9
			  -- 4  16
			  -- 5  25

			  -- 7. Using __call on numbers
			  settypemetatable("number", {
			    __call = function(n, multiplier)
			      return n * multiplier
			    end
			  })
			  print((5)(3))  -- 15

			  -- 8. Extend: add __tostring without removing existing keys
			  extendtypemetatable("number", {
			    __tostring = function(n)
			      return "«" .. n .. "»"
			    end
			  })

			  -- 9. Clear/reset a type metatable
			  settypemetatable("number", nil)
			  -- After reset: tostring(42) returns "42" (default)

			  -- 10. nil __tostring
			  settypemetatable("nil", {
			    __tostring = function() return "N/A" end
			  })
			  print(tostring(nil)) -- "N/A"

			  -- 11. __newindex for strings (intercepts field assignment)
			  settypemetatable("string", {
			    __newindex = function(t, key, value)
			      print("Cannot set " .. key .. " on string")
			    end
			  })
			  local s = "hello"
			  s.custom_field = 42  -- triggers __newindex

			  -- 12. Default string metatable must be restored after clearing
			  settypemetatable("string", nil)
			  -- s:upper() would now fail!
			  settypemetatable("string", { __index = string })
			  -- Now s:upper() works again

			NOTES:
			  - Changes affect ALL values of the type globally in the Lua runtime.
			  - Use with care - inappropriate metamethods can break Lua semantics.
			  - Pass nil to clear/reset a type metatable to default.
			  - extendtypemetatable is additive: it only sets or overwrites specific keys.
			  - Only 5 types are supported: "nil", "boolean", "number", "string", "function".
			    Types like "table", "userdata", "thread" cannot be used.
			  - String type has a default metatable with __index = string table.
			    Clearing it removes all string methods until __index is restored.
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			globals["gettypemetatable"] = new LuaCallbackFunction(GetTypeMetatable);
			globals["settypemetatable"] = new LuaCallbackFunction(SetTypeMetatable);
			globals["extendtypemetatable"] = new LuaCallbackFunction(ExtendTypeMetatable);
			return null;
		}

		private static LuaTuple GetTypeMetatable(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString typeStr)
				return new LuaTuple(LuaNil.Instance);

			var luaType = ParseLuaType(typeStr.Value);
			if (luaType == null)
				return new LuaTuple(LuaNil.Instance);

			if (ctx.State.TypeMetatables.TryGetValue(luaType.Value, out var mt))
				return new LuaTuple(mt.ToTable());

			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple SetTypeMetatable(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString typeStr)
				throw new LuaRuntimeException("settypemetatable(type, [metatable]): first argument must be a type name string.");

			var luaType = ParseLuaType(typeStr.Value);
			if (luaType == null)
				throw new LuaRuntimeException($"settypemetatable: unknown type '{typeStr.Value}'. " +
					"Supported: \"nil\", \"boolean\", \"number\", \"string\", \"function\".");

			if (args.Length >= 2 && args[1] is LuaTable table)
			{
				var mt = LuaMetatable.FromTable(table);
				ctx.State.TypeMetatables[luaType.Value] = mt;
			}
			else
			{
				ctx.State.TypeMetatables.TryRemove(luaType.Value, out _);
			}

			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple ExtendTypeMetatable(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString typeStr)
				throw new LuaRuntimeException("extendtypemetatable(type, extensions): first argument must be a type name string.");

			if (args.Length < 2 || args[1] is not LuaTable extensions)
				throw new LuaRuntimeException("extendtypemetatable(type, extensions): second argument must be a table.");

			var luaType = ParseLuaType(typeStr.Value);
			if (luaType == null)
				throw new LuaRuntimeException($"extendtypemetatable: unknown type '{typeStr.Value}'. " +
					"Supported: \"nil\", \"boolean\", \"number\", \"string\", \"function\".");

			// Get existing metatable as LuaTable, or start with an empty table
			LuaTable existingTable;
			if (ctx.State.TypeMetatables.TryGetValue(luaType.Value, out var existingMt))
				existingTable = existingMt.ToTable();
			else
				existingTable = new LuaTable();

			// Deep merge (non-mutating) - creates a new table
			var mergedTable = existingTable.DeepMergeWith(extensions);

			// Convert back to LuaMetatable and store
			ctx.State.TypeMetatables[luaType.Value] = LuaMetatable.FromTable(mergedTable);

			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaType? ParseLuaType(string typeName)
		{
			return typeName.ToLowerInvariant() switch
			{
				"nil" => LuaType.Nil,
				"boolean" or "bool" => LuaType.Boolean,
				"number" or "num" => LuaType.Number,
				"string" or "str" => LuaType.String,
				"function" or "func" => LuaType.Function,
				_ => null
			};
		}
	}
}
