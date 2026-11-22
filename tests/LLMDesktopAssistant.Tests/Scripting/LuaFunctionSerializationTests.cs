using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua.Converters;
using LLMDesktopAssistant.Scripting.Lua.Store;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Scripting;

public class LuaFunctionSerializationTests
{
	private static LuaState NewState() => new LuaState().LoadDefaultLibraries();

	private static LuaTable ExecuteReturningTable(LuaState state, string code) =>
		(LuaTable)state.Execute(code).First;

	[Fact]
	public void Functions_AreDropped_WithoutConverters()
	{
		var state = NewState();
		var table = ExecuteReturningTable(state, """
			return { counter = 0, bump = function(self) self.counter = self.counter + 1 end }
			""");

		var node = table.ToConstNodeValue(tolerant: true, converters: [])!;
		var dictionary = Assert.IsType<ConstNodeDictionaryValue>(node);

		Assert.True(dictionary.Items.ContainsKey("counter"));
		Assert.False(dictionary.Items.ContainsKey("bump"));
	}

	[Fact]
	public void Function_SurvivesRoundTrip_AndStillReceivesSelf()
	{
		var conv = new LuaFunctionNodeConverter();
		var state = NewState();
		var table = ExecuteReturningTable(state, """
			return {
				counter = 0,
				bump = function(self)
					self.counter = self.counter + 1
					return self.counter
				end
			}
			""");

		var node = table.ToReactiveNodeValue(tolerant: true, converters: [conv])!;
		var revived = Assert.IsType<LuaTable>(node.ToLuaValue(converters: [conv]));
		var bump = Assert.IsType<LuaNativeFunction>(revived.Get("bump"));

		var context = state.CreateContext();
		Assert.Equal(1d, Assert.IsType<LuaNumber>(bump.Invoke(context, revived).First).Value);
		Assert.Equal(2d, Assert.IsType<LuaNumber>(bump.Invoke(context, revived).First).Value);
	}

	[Fact]
	public void CapturedLocal_ComesBackAsNil()
	{
		var conv = new LuaFunctionNodeConverter();
		var state = NewState();
		var table = ExecuteReturningTable(state, """
			local captured = 41
			return { get = function() return captured end }
			""");

		var node = table.ToConstNodeValue(converters: [conv])!;
		var revived = Assert.IsType<LuaTable>(node.ToLuaValue(converters: [conv]));
		var get = Assert.IsType<LuaNativeFunction>(revived.Get("get"));

		// The upvalue slot exists but holds nothing: reading it does not throw, it yields nil.
		Assert.IsType<LuaNil>(get.Invoke(state.CreateContext()).First);
	}

	[Fact]
	public void MarkerString_NeedsTheExplicitFlagToBeRevived()
	{
		var conv = new LuaFunctionNodeConverter();
		var state = NewState();
		var table = ExecuteReturningTable(state, "return { f = function() return 1 end }");

		var node = table.ToConstNodeValue(converters: [conv])!;
		var dumped = Assert.IsType<ConstNodeStringValue>(
			Assert.IsType<ConstNodeDictionaryValue>(node).Items["f"]);

		Assert.StartsWith(LuaFunctionNodeConverter.FunctionMarker, dumped.Value!);
		Assert.IsType<LuaString>(dumped.ToLuaValue(converters: []));
		Assert.IsType<LuaNativeFunction>(dumped.ToLuaValue(converters: [conv]));
	}

	[Fact]
	public void StoreSnapshot_KeepsLuaFunctions_AndRevivesThem()
	{
		var state = NewState();
		var table = ExecuteReturningTable(state, """
			return { counter = 0, bump = function(self) self.counter = self.counter + 1; return self.counter end }
			""");

		var dropped = new AppendOnlyList<string>();
		var snapshot = LuaStoreValueConverter.ToSnapshot(table, dropped);

		Assert.Empty(dropped);
		Assert.IsType<ConstNodeStringValue>(snapshot.Items["bump"]);

		var restored = LuaStoreValueConverter.Restore(snapshot);
		var bump = Assert.IsType<LuaNativeFunction>(restored.Get("bump"));

		Assert.Equal(1d, Assert.IsType<LuaNumber>(bump.Invoke(state.CreateContext(), restored).First).Value);
	}

	[Fact]
	public void StoreSnapshot_StillDropsValuesThatAreNotLuaCode()
	{
		var table = new LuaTable
		{
			["handler"] = new LuaCallbackFunction((ctx, args) => new LuaTuple(LuaNil.Instance)),
			["note"] = new LuaString("kept"),
		};

		var dropped = new AppendOnlyList<string>();
		var snapshot = LuaStoreValueConverter.ToSnapshot(table, dropped);

		Assert.False(snapshot.Items.ContainsKey("handler"));
		Assert.True(snapshot.Items.ContainsKey("note"));
		Assert.Single(dropped);
	}

	[Fact]
	public void UnreadablePayload_StaysAString()
	{
		var conv = new LuaFunctionNodeConverter();
		var text = LuaFunctionNodeConverter.FunctionMarker + "not a dump";
		var node = new ConstNodeStringValue { Value = text };

		// Nothing is lost: the caller keeps the marker, so the next save writes it back verbatim.
		var value = Assert.IsType<LuaString>(node.ToLuaValue(converters: [conv]));
		Assert.Equal(text, value.Value);
	}
}
