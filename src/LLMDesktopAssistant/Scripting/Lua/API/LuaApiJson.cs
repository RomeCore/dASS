using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for JSON encoding/decoding: <c>json.*</c>.
	/// Registered in the global namespace as "json".
	/// Uses System.Text.Json.Nodes for both encoding and decoding.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiJson : LuaApiBase
	{
		public override string? Namespace => "json";

		public override string? Manuals => """
		--- json - JSON encoding and decoding API

		Provides JSON serialization and deserialization functions.
		Handles nil, tables, arrays, strings, numbers and booleans.

		FUNCTIONS:

		--- json.decode(str)
		--- json.parse(str) (alias)
		--- json.deserialize(str) (alias)
		  Parses a strict JSON string into a Lua value.
		  Parameters:
		    - str: string - JSON string to parse
		  Returns: Lua value (table, string, number, boolean, nil)
		  Throws on invalid JSON.

		--- json.decode_tolerant(str)
		--- json.parse_tolerant(str) (alias)
		--- json.deserialize_tolerant(str) (alias)
		  Parses a lenient JSON string using a tolerant parser that handles:
		    - Single-quoted strings ('...')
		    - Missing quotes around keys/values
		    - Trailing commas in arrays/objects
		    - Unclosed brackets
		    - JavaScript comments (// and /* )
		    - NaN, Infinity, +infinity, -infinity
		    - Wrong escape sequences
		    - Unquoted string values
		  Parameters:
		    - str: string - lenient JSON string to parse
		  Returns: Lua value (table, string, number, boolean, nil)
		  Returns nil on parse failure instead of throwing.

		--- json.encode(value, [indented])
		--- json.stringify(value, [indented]) (alias)
		--- json.serialize(value, [indented]) (alias)
		  Serializes a Lua value into a JSON string.
		  Parameters:
		    - value: any - Lua value to serialize (table, string, number, boolean, nil)
		    - indented: boolean - If true, the output will be formatted for readability
		  Returns: string

		NOTES:
		  - Use json.decode_tolerant() for parsing LLM output that often
		    contains malformed JSON (missing quotes, trailing commas, etc.)
		  - Lua tables with sequential integer keys (1..n) are encoded as JSON arrays.
		  - Lua tables with string/non-sequential keys are encoded as JSON objects.
		  - Mixed tables (array part + object part) are encoded as objects,
		    with numeric keys converted to strings.
		  - Userdata, functions, and threads are encoded as null.

		EXAMPLES:

		  -- Decode (strict)
		  local t = json.decode('{"name":"John","age":30}')
		  print(t.name) -- "John"
		  print(t.age)  -- 30

		  -- Decode (tolerant) - handles malformed JSON from LLMs
		  local t = json.decode_tolerant("{ name: 'John', age: 30, }")
		  print(t.name) -- "John"

		  -- Encode
		  local s = json.encode({hello = "world", num = 42})
		  print(s) -- '{"hello":"world","num":42}'

		  -- Encode array
		  local s = json.encode({"a", "b", "c"})
		  print(s) -- '["a","b","c"]'
		""";

		private static readonly JsonSerializerOptions _serializationOptions = new()
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
		};

		private static readonly JsonSerializerOptions _indentedSerializationOptions = new()
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
			WriteIndented = true
		};

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["decode"] = new LuaCallbackFunction(Decode);
			ns["parse"] = ns["decode"];
			ns["deserialize"] = ns["decode"];
			ns["decode_tolerant"] = new LuaCallbackFunction(DecodeTolerant);
			ns["parse_tolerant"] = ns["decode_tolerant"];
			ns["deserialize_tolerant"] = ns["decode_tolerant"];
			ns["encode"] = new LuaCallbackFunction(Encode);
			ns["stringify"] = ns["encode"];
			ns["serialize"] = ns["encode"];
			return null;
		}

		private static LuaTuple Decode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("json.decode(str): at least 1 argument expected.");

				if (args[0] is not LuaString jsonStr)
					throw new LuaRuntimeException($"json.decode(str): expected a string, got {args[0].TypeName}.");

				var node = JsonNode.Parse(jsonStr.Value);
				return new LuaTuple(StructuredLuaConverter.JsonNodeToLuaValue(node));
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"JSON decode error: {ex.Message}");
			}
		}

		private static LuaTuple DecodeTolerant(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("json.decode_tolerant(str): at least 1 argument expected.");

				if (args[0] is not LuaString jsonStr)
					throw new LuaRuntimeException($"json.decode_tolerant(str): expected a string, got {args[0].TypeName}.");

				var node = TolerantJsonParser.Parse(jsonStr.Value);
				return new LuaTuple(StructuredLuaConverter.JsonNodeToLuaValue(node));
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception)
			{
				return new LuaTuple(LuaNil.Instance);
			}
		}

		private static LuaTuple Encode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("json.encode(value, [indented]): at least 1 argument expected.");

				bool indented = false;
				if (args.Length > 1 && args[1] is LuaBoolean bVal && bVal.Value)
					indented = true;

				var value = args[0];
				var node = StructuredLuaConverter.LuaValueToJsonNode(value);
				var options = indented ? _indentedSerializationOptions : _serializationOptions;
				string s = node?.ToJsonString(options) ?? "null";
				return new LuaTuple(new LuaString(s));
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"JSON encode error: {ex.Message}");
			}
		}
	}
}
