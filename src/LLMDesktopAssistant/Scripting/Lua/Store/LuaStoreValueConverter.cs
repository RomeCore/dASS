using System.Collections.Immutable;
using System.Globalization;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Converts between live Lua values and the immutable structured values a store persists.
	/// </summary>
	/// <remarks>
	/// Both directions are depth-limited. The runtime's own deep clone has no cycle guard, so a cyclic
	/// table would not raise a Lua error - it would overflow the stack and take the whole application down.
	/// </remarks>
	public static class LuaStoreValueConverter
	{
		/// <summary>
		/// The maximum nesting depth a value may have on its way into a store.
		/// </summary>
		public const int MaxDepth = 200;

		/// <summary>
		/// Creates a deep copy of the given value, stopping at <see cref="MaxDepth"/> levels.
		/// Functions and userdata are shared, not copied - they cannot be cloned meaningfully.
		/// </summary>
		/// <param name="value">The value to copy.</param>
		/// <returns>An independent copy of every table in the value.</returns>
		public static LuaValue Clone(LuaValue value)
		{
			return CloneCore(value, 0, "$");
		}

		/// <summary>
		/// Converts the given value into the structured form a persisted store keeps.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="depth">The nesting depth of <paramref name="value"/>.</param>
		/// <param name="path">The dotted path used in diagnostics.</param>
		/// <param name="dropped">Collects the paths of the values that cannot be persisted.</param>
		/// <returns>The structured value, or <see langword="null"/> when the value itself was dropped.</returns>
		public static ConstNodeValue? ToSnapshot(LuaValue value, int depth, string path, List<string> dropped)
		{
			ThrowIfTooDeep(depth, path);

			switch (value)
			{
				case LuaNil:
					return ConstNodeNullValue.Instance;
				case LuaBoolean boolean:
					return new ConstNodeBooleanValue { Value = boolean.Value };
				case LuaNumber number:
					return new ConstNodeNumberValue { Value = number.Value };
				case LuaString text:
					return new ConstNodeStringValue { Value = text.Value };
				case LuaTable table:
					return ToSnapshotTable(table, depth, path, dropped);
				default:
					dropped.Add(path);
					return null;
			}
		}

		/// <summary>
		/// Converts the whole key table of a store into a structured dictionary.
		/// </summary>
		/// <param name="data">The live store data.</param>
		/// <param name="dropped">Collects the paths of the values that cannot be persisted.</param>
		/// <returns>The dictionary snapshot, never <see langword="null"/>.</returns>
		public static ConstNodeDictionaryValue ToSnapshot(LuaTable data, List<string> dropped)
		{
			var items = ImmutableDictionary.CreateBuilder<string, ConstNodeValue>();

			foreach (var kvp in data.Entries)
			{
				if (kvp.Key is not LuaString key)
				{
					dropped.Add(kvp.Key.ToString());
					continue;
				}

				if (ToSnapshot(kvp.Value, 0, key.Value, dropped) is { } item)
					items[key.Value] = item;
			}

			return new ConstNodeDictionaryValue { Items = items.ToImmutable() };
		}

		/// <summary>
		/// Restores a live key table from a structured dictionary snapshot.
		/// </summary>
		/// <param name="snapshot">The snapshot to restore, or <see langword="null"/> for an empty store.</param>
		/// <returns>The live key table, never <see langword="null"/>.</returns>
		public static LuaTable Restore(INodeValue? snapshot)
		{
			return snapshot?.ToLuaValue() as LuaTable ?? new LuaTable();
		}

		private static LuaValue CloneCore(LuaValue value, int depth, string path)
		{
			ThrowIfTooDeep(depth, path);

			return value switch
			{
				LuaNil or LuaBoolean => value,
				LuaNumber number => new LuaNumber(number.Value) { Metatable = CloneMetatable(number.Metatable, depth, path) },
				LuaString text => new LuaString(text.Value) { Metatable = CloneMetatable(text.Metatable, depth, path) },
				LuaTable table => CloneTable(table, depth, path),
				_ => value,
			};
		}

		private static LuaTable CloneTable(LuaTable table, int depth, string path)
		{
			var result = new LuaTable
			{
				Metatable = CloneMetatable(table.Metatable, depth, path)
			};

			foreach (var kvp in table.Entries)
			{
				// The global table would drag the whole runtime environment into the store.
				if (kvp.Key is LuaString key && key.Value == "_G")
					continue;

				var childPath = kvp.Key is LuaString name ? $"{path}.{name.Value}" : path;
				result.Set(kvp.Key, CloneCore(kvp.Value, depth + 1, childPath));
			}

			return result;
		}

		private static LuaMetatable? CloneMetatable(LuaMetatable? metatable, int depth, string path)
		{
			if (metatable is null)
				return null;

			ThrowIfTooDeep(depth, path);

			var result = new LuaMetatable();
			foreach (var kvp in metatable)
				result.Set(kvp.Key, CloneCore(kvp.Value, depth + 1, path));

			return result;
		}

		private static ConstNodeValue ToSnapshotTable(LuaTable table, int depth, string path, List<string> dropped)
		{
			if (IsArrayTable(table))
			{
				var items = ImmutableList.CreateBuilder<ConstNodeValue>();

				for (int i = 1; i <= table.Length; i++)
				{
					if (ToSnapshot(table.Get(i), depth + 1, $"{path}[{i}]", dropped) is { } item)
						items.Add(item);
				}

				return new ConstNodeArrayValue { Items = items.ToImmutable() };
			}

			var entries = ImmutableDictionary.CreateBuilder<string, ConstNodeValue>();

			foreach (var kvp in table.Entries)
			{
				var key = KeyToString(kvp.Key);
				if (key is null)
				{
					dropped.Add($"{path}[{kvp.Key}]");
					continue;
				}

				if (ToSnapshot(kvp.Value, depth + 1, $"{path}.{key}", dropped) is { } item)
					entries[key] = item;
			}

			return new ConstNodeDictionaryValue { Items = entries.ToImmutable() };
		}

		private static void ThrowIfTooDeep(int depth, string path)
		{
			if (depth > MaxDepth)
			{
				throw new LuaRuntimeException(
					$"store: value at '{Shorten(path)}' is nested deeper than {MaxDepth} levels (a cyclic table?).");
			}
		}

		// A cyclic value repeats its path hundreds of times; the tail names the place, the head is noise.
		private static string Shorten(string path)
		{
			return path.Length <= 80 ? path : "..." + path[^77..];
		}

		private static bool IsArrayTable(LuaTable table)
		{
			int length = table.Length;

			if (length == 0 && !table.Keys.Any())
				return true;

			if (length == 0)
				return false;

			foreach (var key in table.Keys)
			{
				if (key is not LuaNumber numberKey)
					return false;

				double index = numberKey.Value;
				if (index < 1 || index > length || index != Math.Truncate(index))
					return false;
			}

			return true;
		}

		private static string? KeyToString(LuaValue key)
		{
			return key switch
			{
				LuaString text => text.Value,
				LuaNumber number => number.Value.ToString(CultureInfo.InvariantCulture),
				_ => null,
			};
		}
	}
}
