using System.Globalization;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using YamlDotNet.RepresentationModel;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for YAML encoding/decoding: <c>yaml.*</c>.
	/// Uses the YamlDotNet representation model, so no CLR types are involved in either direction.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiYaml : LuaApiBase
	{
		public override string? Namespace => "yaml";

		public override string? Manuals => """
		--- yaml - YAML encoding and decoding API

		Provides YAML serialization and deserialization functions working on plain Lua values.
		Handles nil, tables, arrays, strings, numbers and booleans.

		FUNCTIONS:

		--- yaml.decode(str)
		  Parses the first YAML document of a string into a Lua value.
		  Parameters:
		    - str: string - YAML text
		  Returns: Lua value (table, string, number, boolean, nil)
		  Throws on malformed YAML.

		--- yaml.decode_all(str)
		  Parses every YAML document of a multi-document string.
		  Parameters:
		    - str: string - YAML text, documents separated by "---"
		  Returns: table - array of documents in order; documents whose root is null are skipped

		--- yaml.encode(value)
		  Serializes a Lua value into a block-style YAML string with a trailing "...".
		  Parameters:
		    - value: any - Lua value to serialize (table, string, number, boolean, nil)
		  Returns: string

		NOTES:
		  - Plain (unquoted) scalars read back as null, boolean or number; quoted and block
		    scalars always read back as strings.
		  - Strings that would read back as null/boolean/number are emitted quoted.
		  - Lua tables with sequential integer keys (1..n) are encoded as sequences.
		  - Lua tables with string/non-sequential keys are encoded as mappings.
		  - Mixed tables (array part + object part) are encoded as mappings,
		    with numeric keys converted to strings.
		  - Values that cannot be encoded (userdata, threads) are dropped; functions are carried
			as marker strings and revived on decode.

		EXAMPLES:

		  -- Decode
		  local t = yaml.decode('name: John\nage: 30\n')
		  print(t.name) -- "John"
		  print(t.age)  -- 30

		  -- Decode a multi-document file
		  local docs = yaml.decode_all('---\nid: 1\n---\nid: 2\n')
		  print(docs[2].id) -- 2

		  -- Encode
		  local s = yaml.encode({hello = "world", num = 42})
		  print(s)
		""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["encode"] = new LuaCallbackFunction(Encode);
			ns["decode_all"] = new LuaCallbackFunction(DecodeAll);
			ns["decode"] = new LuaCallbackFunction(Decode);
			return null;
		}

		private static LuaTuple Encode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("yaml.encode(value): at least 1 argument expected.");

				var node = args[0].ToConstNodeValue(tolerant: true).ToYamlNode();
				var stream = new YamlStream(new YamlDocument(node));

				using var writer = new StringWriter(CultureInfo.InvariantCulture);
				stream.Save(writer, assignAnchors: false);
				return new LuaTuple(new LuaString(writer.ToString()));
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"YAML encode error: {ex.Message}");
			}
		}

		private static LuaTuple Decode(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				var documents = LoadDocuments("yaml.decode", args);
				if (documents.Length == 0)
					return new LuaTuple(LuaNil.Instance);

				return new LuaTuple(documents[0].RootNode.ToConstNodeValue()?.ToLuaValue() ?? LuaNil.Instance);
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"YAML decode error: {ex.Message}");
			}
		}

		private static LuaTuple DecodeAll(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				var documents = LoadDocuments("yaml.decode_all", args);
				var result = LuaExtensions.CreateArrayTable();
				int index = 0;

				foreach (var document in documents)
				{
					var value = document.RootNode.ToConstNodeValue();
					if (value is null or INodeNullValue)
						continue;

					result[++index] = value.ToLuaValue();
				}

				return new LuaTuple(result);
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"YAML decode error: {ex.Message}");
			}
		}

		private static YamlDocument[] LoadDocuments(string function, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException($"{function}(str): at least 1 argument expected.");

			if (args[0] is not LuaString text)
				throw new LuaRuntimeException($"{function}(str): expected a string, got {args[0].TypeName}.");

			var stream = new YamlStream();
			stream.Load(new StringReader(text.Value));
			return [.. stream.Documents];
		}
	}
}
