using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public static class LuaExtensions
	{
		/// <summary>
		/// Determines whether a Lua table should be serialized as a JSON array.
		/// A table is considered an array if it has a contiguous sequence of
		/// integer keys from 1 to <see cref="Table.Length"/> and no keys
		/// outside that range. A truly empty table is an object unless its metatable sets
		/// <c>__json_array</c> to <see langword="true"/>; a table with hash keys is an object regardless.
		/// </summary>
		public static bool IsArrayTable(this LuaTable table)
		{
			int len = table.Length;
			var keys = table.Keys;

			if (len == 0)
			{
				if (keys.Count != 0)
					return false;

				return (table.Metatable?.TryGetValue(new LuaString("__json_array"), out var value) ?? false)
					&& value == LuaBoolean.True;
			}

			for (int i = 1; i <= len; i++)
			{
				if (!keys.Contains(new LuaNumber(i)))
					return false;
			}

			foreach (var key in keys)
			{
				if (key is not LuaNumber numberKey)
					return false;
				double num = numberKey.Value;
				if (num < 1 || num > len || num != Math.Truncate(num))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Creates a table that is considered array when it empty.
		/// This method just creates a table and puts <c>__json_array</c> to <see langword="true"/>
		/// in their metatable.
		/// </summary>
		public static LuaTable CreateArrayTable()
		{
			return new LuaTable
			{
				Metatable = new LuaTable
				{
					["__json_array"] = LuaBoolean.True
				}
			};
		}
	}
}
