using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Tests.StructuredValues
{
	/// <summary>
	/// The conversions the Lua APIs rely on: numbers of any backing type survive a JSON round trip, and
	/// a Lua table that carries something unrepresentable can be converted with the unsupported entries
	/// dropped instead of failing the whole call.
	/// </summary>
	public class StructuredConverterTests
	{
		[Fact]
		public void ToConstNodeValue_KeepsNumbersCreatedFromInts()
		{
			var node = new JsonObject
			{
				["total"] = 19,
				["rolls"] = new JsonArray(1, 2, 3)
			};

			var dictionary = Assert.IsAssignableFrom<INodeDictionaryValue>(node.ToConstNodeValue());

			Assert.Equal(19d, Assert.IsAssignableFrom<INodeNumberValue>(dictionary.Items["total"]).Value);
			Assert.Equal(3, Assert.IsAssignableFrom<INodeArrayValue>(dictionary.Items["rolls"]).Items.Count);
		}

		[Fact]
		public void ToLuaValue_KeepsNumbersOfTheJsonTree()
		{
			var node = new JsonObject { ["total"] = 19 };
			var table = Assert.IsType<LuaTable>(node.ToConstNodeValue().ToLuaValue());

			Assert.Equal(19d, Assert.IsType<LuaNumber>(table["total"]).Value);
		}

		[Fact]
		public void ToConstNodeValue_ThrowsOnUnsupportedLuaValues()
		{
			var table = new LuaTable();
			table["name"] = new LuaString("broken");
			table["callback"] = new LuaCallbackFunction((ctx, args) => new LuaTuple());

			Assert.Throws<ArgumentException>(() => table.ToConstNodeValue());
		}

		[Fact]
		public void ToConstNodeValue_TolerantDropsUnsupportedLuaValues()
		{
			var table = new LuaTable();
			table["name"] = new LuaString("kept");
			table["callback"] = new LuaCallbackFunction((ctx, args) => new LuaTuple());

			var dictionary = Assert.IsAssignableFrom<INodeDictionaryValue>(table.ToConstNodeValue(tolerant: true));

			Assert.True(dictionary.Items.ContainsKey("name"));
			Assert.False(dictionary.Items.ContainsKey("callback"));
		}
	}
}
