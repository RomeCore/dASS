using System.Globalization;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;

namespace LLMDesktopAssistant.StructuredValues.Converters
{
	/// <summary>
	/// Provides conversions between structured node values (<see cref="INodeValue"/>) and
	/// AsyncLua <see cref="LuaValue"/> values.
	/// </summary>
	/// <remarks>
	/// Lua has no null value: nulls are represented as <see cref="LuaNil"/>, and assigning nil to a
	/// table entry removes the entry. As a result, null items of arrays and null values of
	/// dictionaries are dropped when converting to Lua tables and appear as missing entries when
	/// converting back.
	/// </remarks>
	public static class LuaStructuredConverter
	{
		/// <summary>
		/// Converts the given structured node value to a <see cref="LuaValue"/>.
		/// </summary>
		/// <param name="value">The value to convert, or <see langword="null"/>.</param>
		/// <returns>The Lua value (never <see langword="null"/>).</returns>
		public static LuaValue ToLuaValue(this INodeValue? value)
		{
			return value is null ? LuaNil.Instance : ConvertToLua(value);
		}

		/// <summary>
		/// Converts the given Lua value to an immutable structured node value (<see cref="ConstNodeValue"/>).
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <returns>
		/// The immutable node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.
		/// </returns>
		public static ConstNodeValue? ToConstNodeValue(this LuaValue? value)
		{
			return value is null ? null : ConvertToConst(value);
		}

		/// <summary>
		/// Converts the given Lua value to a mutable (reactive) structured node value (<see cref="ReactiveNodeValue"/>).
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <returns>
		/// The reactive node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.
		/// </returns>
		/// <param name="tolerant">
		/// When <see langword="true"/>, unsupported values (functions, userdata, threads, etc.) are
		/// dropped instead of throwing. Containers keep the entries that are representable.
		/// </param>
		public static ReactiveNodeValue? ToReactiveNodeValue(this LuaValue? value, bool tolerant = false)
		{
			return value is null ? null : ConvertToReactive(value, tolerant);
		}

		private static LuaValue ConvertToLua(INodeValue value) => value switch
		{
			INodeNullValue or null => LuaNil.Instance,
			INodeBooleanValue b => LuaBoolean.FromBoolean(b.Value),
			INodeNumberValue n => new LuaNumber(n.Value),
			INodeStringValue s => s.Value is null ? LuaNil.Instance : new LuaString(s.Value),
			INodeArrayValue a => ConvertArrayToLua(a.Items),
			INodeDictionaryValue d => ConvertDictionaryToLua(d.Items),
			_ => throw new ArgumentException($"Unsupported structured node value type '{value.GetType().FullName}'.", nameof(value))
		};

		private static LuaTable ConvertArrayToLua(IReadOnlyList<INodeValue> items)
		{
			var table = new LuaTable();
			for (int i = 0; i < items.Count; i++)
				table[i + 1] = ConvertToLua(items[i]);
			return table;
		}

		private static LuaTable ConvertDictionaryToLua(IReadOnlyDictionary<string, INodeValue> items)
		{
			var table = new LuaTable();
			foreach (var kvp in items)
				table[kvp.Key] = ConvertToLua(kvp.Value);
			return table;
		}

		private static ConstNodeValue ConvertToConst(LuaValue value)
		{
			switch (value)
			{
				case LuaNil:
					return ConstNodeNullValue.Instance;
				case LuaBoolean boolean:
					return new ConstNodeBooleanValue { Value = boolean.Value };
				case LuaNumber number:
					return new ConstNodeNumberValue { Value = number.Value };
				case LuaString str:
					return new ConstNodeStringValue { Value = str.Value };
				case LuaTable table:
				{
					if (IsArrayTable(table))
					{
						return new ConstNodeArrayValue
						{
							Items = Enumerable.Range(1, table.Length).Select(i => ConvertToConst(table.Get(i))).ToImmutableList()
						};
					}
					else
					{
						return new ConstNodeDictionaryValue
						{
							Items = table.Entries
								.Select(kvp => (Key: KeyToString(kvp.Key), Value: kvp.Value))
								.Where(pair => pair.Key is not null)
								.ToImmutableDictionary(pair => pair.Key!, pair => ConvertToConst(pair.Value))
						};
					}
				}
				default:
					throw new ArgumentException($"Unsupported Lua value type '{value.GetType().FullName}'.", nameof(value));
			}
		}

		private static ReactiveNodeValue? ConvertToReactive(LuaValue value, bool tolerant)
		{
			switch (value)
			{
				case LuaNil:
					return new ReactiveNodeNullValue();
				case LuaBoolean boolean:
					return new ReactiveNodeBooleanValue { Value = boolean.Value };
				case LuaNumber number:
					return new ReactiveNodeNumberValue { Value = number.Value };
				case LuaString str:
					return new ReactiveNodeStringValue { Value = str.Value };
				case LuaTable table:
				{
					if (IsArrayTable(table))
					{
						var array = new ReactiveNodeArrayValue();
						for (int i = 1; i <= table.Length; i++)
						{
							var item = ConvertToReactive(table.Get(i), tolerant);
							if (item is not null)
								array.Items.Add(item);
						}
						return array;
					}
					else
					{
						var dictionary = new ReactiveNodeDictionaryValue();
						foreach (var kvp in table.Entries)
						{
							var key = KeyToString(kvp.Key);
							if (key is null)
								continue;
							var item = ConvertToReactive(kvp.Value, tolerant);
							if (item is not null)
								dictionary.Items.Add(key, item);
						}
						return dictionary;
					}
				}
				default:
					if (tolerant)
						return null;
					throw new ArgumentException($"Unsupported Lua value type '{value.GetType().FullName}'.", nameof(value));
			}
		}

		/// <summary>
		/// Determines whether a Lua table is an array (a contiguous sequence of integer keys from 1
		/// to <see cref="LuaTable.Length"/> with no keys outside that range).
		/// </summary>
		private static bool IsArrayTable(LuaTable table)
		{
			int len = table.Length;

			if (len == 0 && table.Keys.Count() == 0)
				return true;

			if (len == 0)
				return false;

			for (int i = 1; i <= len; i++)
			{
				if (!table.Keys.Contains(new LuaNumber(i)))
					return false;
			}

			foreach (var key in table.Keys)
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
		/// Converts a Lua key to a string dictionary key.
		/// Returns <see langword="null"/> for keys that cannot be represented (e.g. table keys).
		/// </summary>
		private static string? KeyToString(LuaValue key)
		{
			switch (key)
			{
				case LuaString str:
					return str.Value;
				case LuaNumber num:
					return num.Value.ToString(CultureInfo.InvariantCulture);
				default:
					return null;
			}
		}
	}
}
