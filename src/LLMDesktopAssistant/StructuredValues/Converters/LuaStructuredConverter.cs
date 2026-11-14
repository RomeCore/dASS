using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using AsyncLua.Interpreting;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua;
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
		public const string FunctionMarker = "\u0000dass:lua-fn:v1:";
		private static readonly ConditionalWeakTable<LuaNativeFunction, string> FunctionDumps = [];

		/// <summary>
		/// Converts the given structured node value to a <see cref="LuaValue"/>.
		/// </summary>
		/// <param name="value">The value to convert, or <see langword="null"/>.</param>
		/// <param name="functions">
		/// When <see langword="true"/>, a string carrying <see cref="FunctionMarker"/> is revived into a
		/// <see cref="LuaNativeFunction"/>. It is opt-in on this side too: data arriving from a model, an
		/// RPC peer or a database must not turn into a callable just because it looks like one.
		/// </param>
		/// <returns>The Lua value (never <see langword="null"/>).</returns>
		public static LuaValue ToLuaValue(this INodeValue? value, bool functions = false)
		{
			return value is null ? LuaNil.Instance : ConvertToLua(value, functions);
		}

		/// <summary>
		/// Converts the given Lua value to an immutable structured node value (<see cref="ConstNodeValue"/>).
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="tolerant">
		/// When <see langword="true"/>, unsupported values (functions, userdata, threads, etc.) are
		/// dropped instead of throwing. Containers keep the entries that are representable.
		/// </param>
		/// <param name="functions">
		/// When <see langword="true"/>, a Lua function is serialized into a marker string
		/// (<see cref="FunctionMarker"/>) instead of being dropped, so the value survives a reload.
		/// </param>
		/// <returns>
		/// The immutable node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>
		/// or, in tolerant mode, the value itself is not representable.
		/// </returns>
		public static ConstNodeValue? ToConstNodeValue(this LuaValue? value, bool tolerant = false, bool functions = false)
		{
			return value is null ? null : ConvertToConst(value, tolerant, functions);
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
		/// <param name="functions">
		/// When <see langword="true"/>, a Lua function is serialized into a marker string
		/// (<see cref="FunctionMarker"/>) instead of being dropped, so the value survives a reload.
		/// </param>
		public static ReactiveNodeValue? ToReactiveNodeValue(this LuaValue? value, bool tolerant = false, bool functions = false)
		{
			return value is null ? null : ConvertToReactive(value, tolerant, functions);
		}

		private static LuaValue ConvertToLua(INodeValue value, bool functions) => value switch
		{
			INodeNullValue or null => LuaNil.Instance,
			INodeBooleanValue b => LuaBoolean.FromBoolean(b.Value),
			INodeNumberValue n => new LuaNumber(n.Value),
			INodeStringValue s => ConvertStringToLua(s, functions),
			INodeArrayValue a => ConvertArrayToLua(a.Items, functions),
			INodeDictionaryValue d => ConvertDictionaryToLua(d.Items, functions),
			_ => throw new ArgumentException($"Unsupported structured node value type '{value.GetType().FullName}'.", nameof(value))
		};

		private static LuaValue ConvertStringToLua(INodeStringValue value, bool functions)
		{
			if (value.Value is null)
				return LuaNil.Instance;

			if (functions && TryReviveFunction(value.Value, out var function))
				return function;

			return new LuaString(value.Value);
		}

		private static LuaTable ConvertArrayToLua(IReadOnlyList<INodeValue> items, bool functions)
		{
			var table = LuaExtensions.CreateArrayTable();
			for (int i = 0; i < items.Count; i++)
				table[i + 1] = ConvertToLua(items[i], functions);
			return table;
		}

		private static LuaTable ConvertDictionaryToLua(IReadOnlyDictionary<string, INodeValue> items, bool functions)
		{
			var table = new LuaTable();
			foreach (var kvp in items)
				table[kvp.Key] = ConvertToLua(kvp.Value, functions);
			return table;
		}

		private static ConstNodeValue? ConvertToConst(LuaValue value, bool tolerant, bool functions)
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
				case LuaNativeFunction function when functions:
					return new ConstNodeStringValue { Value = DumpFunction(function) };
				case LuaTable table:
				{
					if (table.IsArrayTable())
					{
						var items = ImmutableList.CreateBuilder<ConstNodeValue>();

						for (int i = 1; i <= table.Length; i++)
						{
							if (ConvertToConst(table.Get(i), tolerant, functions) is { } item)
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

							if (ConvertToConst(kvp.Value, tolerant, functions) is { } item)
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

		private static ReactiveNodeValue? ConvertToReactive(LuaValue value, bool tolerant, bool functions)
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
				case LuaNativeFunction function when functions:
					return new ReactiveNodeStringValue { Value = DumpFunction(function) };
				case LuaTable table:
				{
					if (table.IsArrayTable())
					{
						var array = new ReactiveNodeArrayValue();
						for (int i = 1; i <= table.Length; i++)
						{
							var item = ConvertToReactive(table.Get(i), tolerant, functions);
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
							var item = ConvertToReactive(kvp.Value, tolerant, functions);
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
		/// Serializes a Lua function into the marker string that carries it through a node tree.
		/// </summary>
		/// <remarks>
		/// Only the prototype travels: upvalues and the per-closure environment stay behind, so the revived
		/// function reads globals from the calling context it is invoked with. Dumps are cached per function
		/// because a widget and a store each re-serialize their whole value on every change.
		/// </remarks>
		/// <param name="function">The function to serialize.</param>
		/// <returns>The marker string, ready to be kept as a <see cref="ConstNodeStringValue"/>.</returns>
		public static string DumpFunction(LuaNativeFunction function)
		{
			return FunctionDumps.GetValue(function, static f =>
				FunctionMarker + Convert.ToBase64String(PrototypeSerializer.Serialize(f.Prototype)));
		}

		/// <summary>
		/// Revives a function serialized by <see cref="DumpFunction"/>.
		/// </summary>
		/// <remarks>
		/// A string that carries the marker but does not deserialize (bytecode from another build, a truncated
		/// payload) stays a string: the caller keeps the original value, so the next save writes it back
		/// verbatim instead of erasing the command from history.
		/// </remarks>
		private static bool TryReviveFunction(string text, [NotNullWhen(true)] out LuaNativeFunction? function)
		{
			function = null;

			if (!text.StartsWith(FunctionMarker, StringComparison.Ordinal))
				return false;

			try
			{
				var bytes = Convert.FromBase64String(text[FunctionMarker.Length..]);
				var prototype = PrototypeSerializer.Deserialize(bytes);

				// A dumped prototype keeps its upvalue count but not their values: every captured variable
				// comes back nil, which is the price of a function outliving the frame that created it.
				var upvalues = prototype.UpvalueDescriptions.Length > 0
					? Array.ConvertAll(prototype.UpvalueDescriptions, _ => Upvalue.CreateClosed(LuaNil.Instance))
					: [];

				function = new LuaNativeFunction(prototype, upvalues);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to revive a serialized Lua function; keeping it as data.");
				return false;
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
