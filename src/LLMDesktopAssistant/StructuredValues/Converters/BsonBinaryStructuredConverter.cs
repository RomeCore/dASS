using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;

namespace LLMDesktopAssistant.StructuredValues.Converters
{
	/// <summary>
	/// Provides conversions between structured node values (<see cref="INodeValue"/>) and
	/// BSON binary documents (<see cref="byte"/> arrays).
	/// </summary>
	/// <remarks>
	/// Only the value kinds the structured model carries are supported: null, boolean, number, string, array and
	/// document. Numbers are always written as BSON doubles, while int32 and int64 elements read back as doubles.
	/// The root element is always a document, because BSON cannot mark a top-level value as an array.
	/// </remarks>
	public static class BsonBinaryStructuredConverter
	{
		private const byte typeDouble = 0x01;
		private const byte typeString = 0x02;
		private const byte typeDocument = 0x03;
		private const byte typeArray = 0x04;
		private const byte typeBoolean = 0x08;
		private const byte typeNull = 0x0A;
		private const byte typeInt32 = 0x10;
		private const byte typeInt64 = 0x12;

		/// <summary>
		/// Converts the given structured node value to a BSON document.
		/// </summary>
		/// <param name="value">
		/// The value to convert, or <see langword="null"/>. A null value has no BSON representation and is rejected.
		/// </param>
		/// <returns>The BSON bytes.</returns>
		public static byte[] ToBsonBytes(this INodeValue? value)
		{
			if (value is not INodeDictionaryValue document)
				throw new ArgumentException("The BSON root element must be a document (a table with string keys).", nameof(value));

			using var stream = new MemoryStream();
			WriteDocument(stream, document.Items);
			return stream.ToArray();
		}

		/// <summary>
		/// Converts the given BSON document to an immutable structured node value (<see cref="ConstNodeValue"/>).
		/// </summary>
		/// <param name="value">The BSON bytes to convert, or <see langword="null"/>.</param>
		/// <returns>
		/// The immutable node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.
		/// </returns>
		public static ConstNodeValue? ToConstNodeValue(this byte[]? value)
		{
			if (value is null)
				return null;

			int offset = 0;
			var document = ReadDocument(value, ref offset);
			if (offset != value.Length)
				throw new FormatException($"BSON document is {value.Length} bytes long, but its root element declares {offset}.");

			return document;
		}

		/// <summary>
		/// Converts the given BSON document to a mutable (reactive) structured node value (<see cref="ReactiveNodeValue"/>).
		/// </summary>
		/// <param name="value">The BSON bytes to convert, or <see langword="null"/>.</param>
		/// <returns>
		/// The reactive node value, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.
		/// </returns>
		public static ReactiveNodeValue? ToReactiveNodeValue(this byte[]? value)
		{
			return value?.ToConstNodeValue().ToReactiveNodeValue();
		}

		private static void WriteDocument(Stream stream, IReadOnlyDictionary<string, INodeValue> items)
		{
			using var body = new MemoryStream();
			foreach (var kvp in items)
				WriteElement(body, kvp.Key, kvp.Value);
			body.WriteByte(0);

			var bytes = body.ToArray();
			WriteInt32(stream, bytes.Length + 4);
			stream.Write(bytes);
		}

		private static void WriteElement(Stream stream, string name, INodeValue? value)
		{
			switch (value)
			{
				case null or INodeNullValue:
					stream.WriteByte(typeNull);
					WriteCString(stream, name);
					break;
				case INodeBooleanValue boolean:
					stream.WriteByte(typeBoolean);
					WriteCString(stream, name);
					stream.WriteByte(boolean.Value ? (byte)1 : (byte)0);
					break;
				case INodeNumberValue number:
					stream.WriteByte(typeDouble);
					WriteCString(stream, name);
					WriteDouble(stream, number.Value);
					break;
				case INodeStringValue text:
					stream.WriteByte(typeString);
					WriteCString(stream, name);
					WriteString(stream, text.Value ?? string.Empty);
					break;
				case INodeArrayValue array:
					stream.WriteByte(typeArray);
					WriteCString(stream, name);
					WriteDocument(stream, ToIndexedItems(array.Items));
					break;
				case INodeDictionaryValue document:
					stream.WriteByte(typeDocument);
					WriteCString(stream, name);
					WriteDocument(stream, document.Items);
					break;
				default:
					throw new ArgumentException($"Unsupported structured node value type '{value.GetType().FullName}'.", nameof(value));
			}
		}

		private static Dictionary<string, INodeValue> ToIndexedItems(IReadOnlyList<INodeValue> items)
		{
			var result = new Dictionary<string, INodeValue>(items.Count);
			for (int i = 0; i < items.Count; i++)
				result[i.ToString(CultureInfo.InvariantCulture)] = items[i];
			return result;
		}

		private static void WriteCString(Stream stream, string name)
		{
			if (name.Contains('\0'))
				throw new ArgumentException("A BSON element name cannot contain a null character.", nameof(name));

			stream.Write(Encoding.UTF8.GetBytes(name));
			stream.WriteByte(0);
		}

		private static void WriteString(Stream stream, string text)
		{
			var bytes = Encoding.UTF8.GetBytes(text);
			WriteInt32(stream, bytes.Length + 1);
			stream.Write(bytes);
			stream.WriteByte(0);
		}

		private static void WriteInt32(Stream stream, int value)
		{
			Span<byte> buffer = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
			stream.Write(buffer);
		}

		private static void WriteDouble(Stream stream, double value)
		{
			Span<byte> buffer = stackalloc byte[8];
			BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
			stream.Write(buffer);
		}

		private static ConstNodeValue ReadDocument(ReadOnlySpan<byte> bytes, ref int offset)
		{
			var elements = ReadElements(bytes, ref offset);
			return new ConstNodeDictionaryValue
			{
				Items = elements.ToImmutableDictionary(pair => pair.Name, pair => pair.Value)
			};
		}

		private static ConstNodeValue ReadArray(ReadOnlySpan<byte> bytes, ref int offset)
		{
			var elements = ReadElements(bytes, ref offset);
			return new ConstNodeArrayValue
			{
				Items = elements.Select(pair => pair.Value).ToImmutableList()
			};
		}

		/// <summary>
		/// Reads the elements of a document together with their names, keeping the on-wire order.
		/// </summary>
		/// <param name="bytes">The BSON bytes.</param>
		/// <param name="offset">The read position, advanced past the document.</param>
		/// <returns>The elements in the order they appear.</returns>
		private static List<(string Name, ConstNodeValue Value)> ReadElements(ReadOnlySpan<byte> bytes, ref int offset)
		{
			var start = offset;
			var length = ReadInt32(bytes, ref offset);
			if (length < 5 || start + length > bytes.Length)
				throw new FormatException($"BSON document declares an invalid length of {length} bytes.");

			var end = start + length;
			var elements = new List<(string Name, ConstNodeValue Value)>();

			while (true)
			{
				if (offset >= end)
					throw new FormatException("BSON document ends without a terminator.");

				var type = bytes[offset++];
				if (type == 0)
					break;

				var name = ReadCString(bytes, ref offset, end);
				elements.Add((name, ReadValue(type, bytes, ref offset, end)));
			}

			if (offset != end)
				throw new FormatException($"BSON document declares {length} bytes, but its elements end at {offset - start}.");

			return elements;
		}

		private static ConstNodeValue ReadValue(byte type, ReadOnlySpan<byte> bytes, ref int offset, int end) => type switch
		{
			typeDouble => new ConstNodeNumberValue { Value = ReadDouble(bytes, ref offset) },
			typeString => new ConstNodeStringValue { Value = ReadString(bytes, ref offset, end) },
			typeDocument => ReadDocument(bytes, ref offset),
			typeArray => ReadArray(bytes, ref offset),
			typeBoolean => new ConstNodeBooleanValue { Value = ReadBoolean(bytes, ref offset) },
			typeNull => ConstNodeNullValue.Instance,
			typeInt32 => new ConstNodeNumberValue { Value = ReadInt32(bytes, ref offset) },
			typeInt64 => new ConstNodeNumberValue { Value = ReadInt64(bytes, ref offset) },
			_ => throw new FormatException($"Unsupported BSON element type 0x{type:X2}.")
		};

		private static string ReadCString(ReadOnlySpan<byte> bytes, ref int offset, int end)
		{
			var terminator = bytes[offset..end].IndexOf((byte)0);
			if (terminator < 0)
				throw new FormatException("BSON element name is not null-terminated.");

			var name = Encoding.UTF8.GetString(bytes.Slice(offset, terminator));
			offset += terminator + 1;
			return name;
		}

		private static string ReadString(ReadOnlySpan<byte> bytes, ref int offset, int end)
		{
			var length = ReadInt32(bytes, ref offset);
			if (length < 1 || offset + length > end)
				throw new FormatException($"BSON string declares an invalid length of {length} bytes.");
			if (bytes[offset + length - 1] != 0)
				throw new FormatException("BSON string is not null-terminated.");

			var text = Encoding.UTF8.GetString(bytes.Slice(offset, length - 1));
			offset += length;
			return text;
		}

		private static bool ReadBoolean(ReadOnlySpan<byte> bytes, ref int offset)
		{
			EnsureAvailable(bytes, offset, 1);
			var value = bytes[offset++];
			return value switch
			{
				0 => false,
				1 => true,
				_ => throw new FormatException($"BSON boolean must be 0 or 1, got {value}.")
			};
		}

		private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset)
		{
			EnsureAvailable(bytes, offset, 4);
			var value = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
			offset += 4;
			return value;
		}

		private static long ReadInt64(ReadOnlySpan<byte> bytes, ref int offset)
		{
			EnsureAvailable(bytes, offset, 8);
			var value = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset, 8));
			offset += 8;
			return value;
		}

		private static double ReadDouble(ReadOnlySpan<byte> bytes, ref int offset)
		{
			EnsureAvailable(bytes, offset, 8);
			var value = BinaryPrimitives.ReadDoubleLittleEndian(bytes.Slice(offset, 8));
			offset += 8;
			return value;
		}

		private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int count)
		{
			if (offset < 0 || offset + count > bytes.Length)
				throw new FormatException("BSON data ends in the middle of an element.");
		}
	}
}
