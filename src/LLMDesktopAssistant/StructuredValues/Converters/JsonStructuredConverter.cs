using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;

namespace LLMDesktopAssistant.StructuredValues.Converters
{
	/// <summary>
	/// Provides conversions between structured node values (<see cref="INodeValue"/>) and
	/// <see cref="JsonNode"/> trees of the System.Text.Json object model.
	/// </summary>
	public static class JsonStructuredConverter
	{
		/// <summary>
		/// Converts the given structured node value to a <see cref="JsonNode"/> tree.
		/// </summary>
		/// <param name="value">The value to convert, or <see langword="null"/>.</param>
		/// <returns>
		/// The JSON node, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>
		/// or represents a null value.
		/// </returns>
		public static JsonNode? ToJsonNode(this INodeValue? value)
		{
			return value is null ? null : ConvertToJson(value);
		}

		/// <summary>
		/// Converts the given JSON node to an immutable structured node value (<see cref="ConstNodeValue"/>).
		/// </summary>
		/// <param name="node">The node to convert, or <see langword="null"/>.</param>
		/// <returns>
		/// The immutable node value, or <see langword="null"/> if <paramref name="node"/> is <see langword="null"/>.
		/// </returns>
		public static ConstNodeValue? ToConstNodeValue(this JsonNode? node)
		{
			return node is null ? null : ConvertToConst(node);
		}

		/// <summary>
		/// Converts the given JSON node to a mutable (reactive) structured node value (<see cref="ReactiveNodeValue"/>).
		/// </summary>
		/// <param name="node">The node to convert, or <see langword="null"/>.</param>
		/// <returns>
		/// The reactive node value, or <see langword="null"/> if <paramref name="node"/> is <see langword="null"/>.
		/// </returns>
		public static ReactiveNodeValue? ToReactiveNodeValue(this JsonNode? node)
		{
			return node is null ? null : ConvertToReactive(node);
		}

		private static JsonNode? ConvertToJson(INodeValue value) => value switch
		{
			INodeNullValue or null => null,
			INodeBooleanValue b => JsonValue.Create(b.Value),
			INodeNumberValue n => JsonValue.Create(n.Value),
			INodeStringValue s => JsonValue.Create(s.Value),
			INodeArrayValue a => new JsonArray(a.Items.Select(ConvertToJson).ToArray()),
			INodeDictionaryValue d => ConvertDictionaryToJsonObject(d.Items),
			_ => throw new ArgumentException($"Unsupported structured node value type '{value.GetType().FullName}'.", nameof(value))
		};

		private static JsonObject ConvertDictionaryToJsonObject(IReadOnlyDictionary<string, INodeValue> items)
		{
			var obj = new JsonObject();
			foreach (var kvp in items)
				obj.Add(kvp.Key, ConvertToJson(kvp.Value));
			return obj;
		}

		private static ConstNodeValue ConvertToConst(JsonNode node) => node switch
		{
			JsonObject obj => new ConstNodeDictionaryValue
			{
				Items = obj.ToImmutableDictionary(p => p.Key, p => p.Value is null ? ConstNodeNullValue.Instance : ConvertToConst(p.Value))
			},
			JsonArray arr => new ConstNodeArrayValue
			{
				Items = arr.Select(item => item is null ? ConstNodeNullValue.Instance : ConvertToConst(item)).ToImmutableList()
			},
			JsonValue val => ConvertToConst(val),
			_ => throw new ArgumentException($"Unsupported JSON node type '{node.GetType().FullName}'.", nameof(node))
		};

		private static ReactiveNodeValue ConvertToReactive(JsonNode node)
		{
			switch (node)
			{
				case JsonObject obj:
				{
					var dictionary = new ReactiveNodeDictionaryValue();
					foreach (var prop in obj)
						dictionary.Items.Add(prop.Key, prop.Value is null ? new ReactiveNodeNullValue() : ConvertToReactive(prop.Value));
					return dictionary;
				}
				case JsonArray arr:
				{
					var array = new ReactiveNodeArrayValue();
					foreach (var item in arr)
						array.Items.Add(item is null ? new ReactiveNodeNullValue() : ConvertToReactive(item));
					return array;
				}
				case JsonValue val:
					return ConvertToReactive(val);
				default:
					throw new ArgumentException($"Unsupported JSON node type '{node.GetType().FullName}'.", nameof(node));
			}
		}

		private static ConstNodeValue ConvertToConst(JsonValue value) => value.GetValueKind() switch
		{
			JsonValueKind.Null => ConstNodeNullValue.Instance,
			JsonValueKind.String => new ConstNodeStringValue { Value = value.GetValue<string>() },
			JsonValueKind.Number => new ConstNodeNumberValue { Value = ReadNumber(value) },
			JsonValueKind.True or JsonValueKind.False => new ConstNodeBooleanValue { Value = value.GetValue<bool>() },
			_ => throw new ArgumentException($"Unsupported JSON value kind '{value.GetValueKind()}'.", nameof(value))
		};

		/// <summary>
		/// Reads a JSON number regardless of the CLR type it was created from: a hand-built
		/// <c>JsonObject</c> usually carries an <see cref="int"/> or <see cref="long"/>, and probing for
		/// one concrete type would miss the others.
		/// </summary>
		/// <param name="value">The JSON value known to be a number.</param>
		/// <returns>The number as a double, which is what structured values store.</returns>
		private static double ReadNumber(JsonValue value)
		{
			if (value.TryGetValue(out double number))
				return number;

			return double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
		}

		private static ReactiveNodeValue ConvertToReactive(JsonValue value) => value.GetValueKind() switch
		{
			JsonValueKind.Null => new ReactiveNodeNullValue(),
			JsonValueKind.String => new ReactiveNodeStringValue { Value = value.GetValue<string>() },
			JsonValueKind.Number => new ReactiveNodeNumberValue { Value = ReadNumber(value) },
			JsonValueKind.True or JsonValueKind.False => new ReactiveNodeBooleanValue { Value = value.GetValue<bool>() },
			_ => throw new ArgumentException($"Unsupported JSON value kind '{value.GetValueKind()}'.", nameof(value))
		};
	}
}
