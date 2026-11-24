using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for BSON encoding/decoding: <c>bson.*</c>.
	/// Supports both the byte-table form the rest of the Lua API uses for binary data and the text forms
	/// (Base64 by default, hex on request).
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiBson : LuaApiBase
	{
		private const string formatBase64 = "base64";
		private const string formatHex = "hex";
		private const string formatBytes = "bytes";

		public override string? Namespace => "bson";

		public override string? Manuals => """
		--- bson - BSON encoding and decoding API

		BSON is the binary document format used by MongoDB and LiteDB. Unlike JSON, it is a binary
		format, so the output is returned as Base64 text or as a table of bytes.

		FUNCTIONS:

		--- bson.encode(value, [format])
		  Serializes a Lua table into a BSON document.
		  Parameters:
		    - value: table - the document to serialize; the root must be a table with string keys
		    - format: string - "base64" (default), "hex" or "bytes"
		  Returns: string for "base64"/"hex", a table of integers (0-255) for "bytes"
		  The root is always a document: BSON has no way to mark a top-level value as an array.
		  Values that cannot be represented (userdata, threads) are dropped; functions are carried
		  as marker strings and revived on decode.

		--- bson.decode(data, [format])
		  Parses a BSON document into a Lua value.
		  Parameters:
		    - data: string or table - Base64/hex text, or a table of bytes (the table form
		      works for any format value)
		    - format: string - how the string is encoded: "base64" (default) or "hex"
		  Returns: table
		  Throws on malformed BSON and on unsupported element types.

		NOTES:
		  - Numbers are written as BSON doubles; int32 and int64 elements read back as numbers.
		  - BSON types the structured model cannot carry (ObjectId, Decimal128, binary, dates,
		    regular expressions) are rejected on decode.

		EXAMPLES:

		  -- Encode to Base64
		  local s = bson.encode({name = "John", age = 30})
		  print(s)

		  -- Encode to raw bytes and write them to a file
		  local bytes = bson.encode({kind = "blob", size = 3}, "bytes")
		  fs.write_binary("doc.bson", bytes)

		  -- Decode back
		  local doc = bson.decode(s)
		  print(doc.name) -- "John"

		  -- Round trip through a file
		  local doc = bson.decode(fs.read_binary("doc.bson"))
		  print(doc.kind) -- "blob"
		""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["encode"] = new LuaCallbackFunction(Encode);
			ns["decode"] = new LuaCallbackFunction(Decode);
			return null;
		}

		private static LuaTuple Encode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("bson.encode(value, [format]): at least 1 argument expected.");

				var format = GetFormat("bson.encode", args);
				var value = args[0].ToConstNodeValue(tolerant: true);
				if (value is null)
					throw new LuaRuntimeException($"bson.encode(value): value of type '{args[0].TypeName}' cannot be encoded.");

				return new LuaTuple(FormatBytes(value.ToBsonBytes(), format));
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"BSON encode error: {ex.Message}");
			}
		}

		private static LuaTuple Decode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("bson.decode(data, [format]): at least 1 argument expected.");

				var format = GetFormat("bson.decode", args);
				var node = ReadBytes(args[0], format).ToConstNodeValue();
				return new LuaTuple(node.ToLuaValue());
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"BSON decode error: {ex.Message}");
			}
		}

		private static string GetFormat(string function, LuaValue[] args)
		{
			if (args.Length < 2)
				return formatBase64;

			if (args[1] is not LuaString formatValue)
				throw new LuaRuntimeException($"{function}(): format must be a string: \"{formatBase64}\", \"{formatHex}\" or \"{formatBytes}\".");

			var format = formatValue.Value.ToLowerInvariant();
			return format is formatBase64 or formatHex or formatBytes
				? format
				: throw new LuaRuntimeException($"{function}(): unknown format '{formatValue.Value}'. Supported: {formatBase64}, {formatHex}, {formatBytes}.");
		}

		private static LuaValue FormatBytes(byte[] bytes, string format) => format switch
		{
			formatHex => new LuaString(Convert.ToHexStringLower(bytes)),
			formatBytes => ToByteTable(bytes),
			_ => new LuaString(Convert.ToBase64String(bytes)),
		};

		private static LuaTable ToByteTable(byte[] bytes)
		{
			var table = LuaExtensions.CreateArrayTable();
			for (int i = 0; i < bytes.Length; i++)
				table[i + 1] = new LuaNumber(bytes[i]);
			return table;
		}

		private static byte[] ReadBytes(LuaValue value, string format)
		{
			if (value is LuaTable table)
				return FromByteTable(table);

			if (value is not LuaString text)
				throw new LuaRuntimeException("bson.decode(data): data must be a string or a table of bytes.");

			if (format == formatBytes)
				throw new LuaRuntimeException("bson.decode(data, \"bytes\"): raw bytes must be passed as a table of integers, not as a string.");

			return format == formatHex
				? Convert.FromHexString(text.Value)
				: Convert.FromBase64String(text.Value);
		}

		private static byte[] FromByteTable(LuaTable table)
		{
			var bytes = new byte[table.Length];
			for (int i = 0; i < bytes.Length; i++)
			{
				if (table.Get(i + 1) is not LuaNumber number ||
					number.Value < byte.MinValue ||
					number.Value > byte.MaxValue ||
					number.Value != Math.Truncate(number.Value))
				{
					throw new LuaRuntimeException($"bson.decode(data): item {i + 1} of the byte table must be an integer 0-255.");
				}

				bytes[i] = (byte)number.Value;
			}

			return bytes;
		}
	}
}
