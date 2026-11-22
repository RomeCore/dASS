using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using AsyncLua.Interpreting;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.Converters;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;
using Serilog;

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
		/// <param name="converters">
		/// The converters to use.
		/// When <see langword="null"/>, converters from <see cref="LuaValueNodeConverterRegistry"/> are used.
		/// </param>
		/// <returns>The Lua value (never <see langword="null"/>).</returns>
		public static LuaValue ToLuaValue(this INodeValue? value,
			IEnumerable<LuaValueNodeConverter>? converters = null)
		{
			var array = converters is not null ? converters.ToImmutableArray() : LuaValueNodeConverterRegistry.Converters;
			return value is null ? LuaNil.Instance : ConvertToLua(value, array);
		}

		/// <summary>
		/// Converts the given Lua value to an immutable structured node value (<see cref="ConstNodeValue"/>).
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="tolerant">
		/// When <see langword="true"/>, unsupported values (functions, userdata, threads, etc.) are
		/// dropped instead of throwing. Containers keep the entries that are representable.
		/// </param>
		/// <param name="converters">
		/// The converters to use.
		/// When <see langword="null"/>, converters from <see cref="LuaValueNodeConverterRegistry"/> are used.
		/// </param>
		/// <returns>
		/// The immutable node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>
		/// or, in tolerant mode, the value itself is not representable.
		/// </returns>
		public static ConstNodeValue? ToConstNodeValue(this LuaValue? value, bool tolerant = false,
			IEnumerable<LuaValueNodeConverter>? converters = null)
		{
			var array = converters is not null ? converters.ToImmutableArray() : LuaValueNodeConverterRegistry.Converters;
			return value is null ? null : ConvertToConst(value, tolerant, array);
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
		/// <param name="converters">
		/// The converters to use.
		/// When <see langword="null"/>, converters from <see cref="LuaValueNodeConverterRegistry"/> are used.
		/// </param>
		public static ReactiveNodeValue? ToReactiveNodeValue(this LuaValue? value, bool tolerant = false,
			IEnumerable<LuaValueNodeConverter>? converters = null)
		{
			var array = converters is not null ? converters.ToImmutableArray() : LuaValueNodeConverterRegistry.Converters;
			return value is null ? null : ConvertToReactive(value, tolerant, array);
		}

		private static LuaValue ConvertToLua(INodeValue value, ImmutableArray<LuaValueNodeConverter> converters)
		{
			foreach (var converter in converters)
				if (converter.TryConvertToLua(value) is { } result)
					return result;

			return value switch
			{
				INodeNullValue or null => LuaNil.Instance,
				INodeBooleanValue b => LuaBoolean.FromBoolean(b.Value),
				INodeNumberValue n => new LuaNumber(n.Value),
				INodeStringValue s => new LuaString(s.Value ?? string.Empty),
				INodeArrayValue a => ConvertArrayToLua(a.Items, converters),
				INodeDictionaryValue d => ConvertDictionaryToLua(d.Items, converters),
				_ => throw new ArgumentException($"Unsupported structured node value type '{value.GetType().FullName}'.", nameof(value))
			};
		}

		private static LuaTable ConvertArrayToLua(IReadOnlyList<INodeValue> items,
			ImmutableArray<LuaValueNodeConverter> converters)
		{
			var table = LuaExtensions.CreateArrayTable();
			for (int i = 0; i < items.Count; i++)
				table[i + 1] = ConvertToLua(items[i], converters);
			return table;
		}

		private static LuaTable ConvertDictionaryToLua(IReadOnlyDictionary<string, INodeValue> items,
			ImmutableArray<LuaValueNodeConverter> converters)
		{
			var table = new LuaTable();
			foreach (var kvp in items)
				table[kvp.Key] = ConvertToLua(kvp.Value, converters);
			return table;
		}

		private static ConstNodeValue? ConvertToConst(LuaValue value, bool tolerant,
			ImmutableArray<LuaValueNodeConverter> converters)
		{
			foreach (var converter in converters)
				if (converter.TryConvertFromLua(value, isConst: true) is { } nodeValue)
				{
					if (nodeValue is ConstNodeValue result)
						return result;
					return nodeValue.ToConstNodeValue();
				}

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
					if (table.IsArrayTable())
					{
						var items = ImmutableList.CreateBuilder<ConstNodeValue>();

						for (int i = 1; i <= table.Length; i++)
						{
							if (ConvertToConst(table.Get(i), tolerant, converters) is { } item)
								items.Add(item);
						}

						return new ConstNodeArrayValue { Items = items.ToImmutable() };
					}
					else
					{
						var items = ImmutableDictionary.CreateBuilder<string, ConstNodeValue>();

						foreach (var kvp in table.Entries)
						{
							var key = KeyToString(kvp.Key);
							if (key is null)
								continue;

							if (ConvertToConst(kvp.Value, tolerant, converters) is { } item)
								items[key] = item;
						}

						return new ConstNodeDictionaryValue { Items = items.ToImmutable() };
					}
				}
				default:
					if (tolerant)
						return null;

					throw new ArgumentException($"Unsupported Lua value type '{value.GetType().FullName}'.", nameof(value));
			}
		}

		private static ReactiveNodeValue? ConvertToReactive(LuaValue value, bool tolerant,
			ImmutableArray<LuaValueNodeConverter> converters)
		{
			foreach (var converter in converters)
				if (converter.TryConvertFromLua(value, isConst: false) is { } nodeValue)
				{
					if (nodeValue is ReactiveNodeValue result)
						return result;
					return nodeValue.ToReactiveNodeValue();
				}

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
					if (table.IsArrayTable())
					{
						var array = new ReactiveNodeArrayValue();
						for (int i = 1; i <= table.Length; i++)
						{
							var item = ConvertToReactive(table.Get(i), tolerant, converters);
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
							var item = ConvertToReactive(kvp.Value, tolerant, converters);
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
