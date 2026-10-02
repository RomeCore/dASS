using AsyncLua;
using AsyncLua.Values;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.MVVM.Dynamic;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.StructuredValues.Reactive;

namespace LLMDesktopAssistant.Tests.MVVM.Dynamic;

/// <summary>
/// Smoke tests for the structured dynamic view models: member projection, nesting, writes,
/// array access, Lua helper injection, the <c>__changed</c> callback and Lua-function commands.
/// </summary>
public class StructuredDynamicViewModelTests
{
	private static ReactiveNodeDictionaryValue Dictionary(params (string Key, ReactiveNodeValue Value)[] pairs)
	{
		var dictionary = new ReactiveNodeDictionaryValue();
		foreach (var (key, value) in pairs)
			dictionary.Items.Add(key, value);
		return dictionary;
	}

	[Fact]
	public void NodeDictionary_ProjectsScalarsAndNestedContainers()
	{
		var root = Dictionary(
			("title", new ReactiveNodeStringValue { Value = "demo" }),
			("count", new ReactiveNodeNumberValue { Value = 3 }),
			("enabled", new ReactiveNodeBooleanValue { Value = true }),
			("nothing", new ReactiveNodeNullValue()),
			("nested", Dictionary(("value", new ReactiveNodeNumberValue { Value = 7 }))));

		var vm = new NodeValueDynamicViewModel(root);

		Assert.Equal("demo", vm.GetDynamicMember("title"));
		Assert.Equal(3d, vm.GetDynamicMember("count"));
		Assert.Equal(true, vm.GetDynamicMember("enabled"));
		Assert.Null(vm.GetDynamicMember("nothing"));

		var nested = Assert.IsType<NodeValueDynamicViewModel>(vm.GetDynamicMember("nested"));
		Assert.Equal(7d, nested.GetDynamicMember("value"));
		Assert.Same(nested, vm.GetDynamicMember("nested"));

		Assert.Equal(5, vm.Count);
		Assert.Contains("title", vm.Keys);
		Assert.Contains("nested", vm.Keys);
	}

	[Fact]
	public void NodeDictionary_WritesAndPropagatesScalarChanges()
	{
		var root = Dictionary(("count", new ReactiveNodeNumberValue { Value = 1 }));
		var vm = new NodeValueDynamicViewModel(root);

		var changed = new List<string?>();
		vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		Assert.Equal(1d, vm.GetDynamicMember("count"));

		// A write replaces the entry with a fresh reactive node.
		vm.SetDynamicMember("count", 5d);
		Assert.Equal(5d, vm.GetDynamicMember("count"));
		var current = Assert.IsType<ReactiveNodeNumberValue>(root.Items["count"]);
		Assert.Equal(5d, current.Value);

		changed.Clear();
		current.Value = 9d;
		Assert.Equal(9d, vm.GetDynamicMember("count"));
		Assert.Contains("count", changed);
	}

	[Fact]
	public void NodeArray_ExposesIndexerAndItemsView()
	{
		var array = new ReactiveNodeArrayValue();
		array.Items.Add(new ReactiveNodeStringValue { Value = "a" });
		array.Items.Add(new ReactiveNodeStringValue { Value = "b" });

		var vm = new NodeValueDynamicViewModel(array);

		Assert.Equal(2, vm.Count);
		Assert.Equal(2, vm.Items.Count);
		Assert.Equal("a", vm.GetDynamicMember("0"));
		Assert.Equal("b", vm.Items[1]);

		vm.SetDynamicMember("0", "z");
		Assert.Equal("z", vm.Items[0]);
	}

	[Fact]
	public void LuaTable_ProjectsMembersAndHidesServiceKeys()
	{
		var state = new LuaState();
		var context = state.CreateContext();
		var table = new LuaTable();
		table.Set("name", new LuaString("demo"));
		table.Set("value", new LuaNumber(42));
		table.Set("nested", Dictionary(("inner", new ReactiveNodeNumberValue { Value = 1 })).ToLuaValue());

		var vm = new LuaValueDynamicViewModel(table, context);

		Assert.Equal("demo", vm.GetDynamicMember("name"));
		Assert.Equal(42d, vm.GetDynamicMember("value"));
		Assert.IsType<LuaValueDynamicViewModel>(vm.GetDynamicMember("nested"));

		Assert.DoesNotContain(LuaValueDynamicViewModel.SetKey, vm.Keys);
		Assert.DoesNotContain(LuaValueDynamicViewModel.NotifyKey, vm.Keys);
		Assert.DoesNotContain(LuaValueDynamicViewModel.ChangedKey, vm.Keys);
		Assert.Null(vm.GetDynamicMember(LuaValueDynamicViewModel.SetKey));
		Assert.Equal(3, vm.Count);
	}

	[Fact]
	public void LuaTable_SetHelperWritesBackToTable()
	{
		var state = new LuaState();
		var context = state.CreateContext();
		var table = new LuaTable();
		var vm = new LuaValueDynamicViewModel(table, context);

		var setFunction = Assert.IsType<LuaFunction>(table.Get(LuaValueDynamicViewModel.SetKey), exactMatch: false);
		setFunction.Invoke(context, table, new LuaString("created"), new LuaNumber(7));

		Assert.Equal(7d, table.Get("created").TryToNumber()!.Value);
		Assert.Equal(7d, vm.GetDynamicMember("created"));
	}

	[Fact]
	public void LuaTable_SetRaisesNotifyAndChangedCallback()
	{
		var state = new LuaState();
		var context = state.CreateContext();
		var table = new LuaTable();
		var changed = new List<string?>();
		table.Set(LuaValueDynamicViewModel.ChangedKey, new LuaCallbackFunction((_, args) =>
		{
			changed.Add(args.Length > 1 ? args[1].ToString() : null);
			return LuaTuple.Empty;
		}));

		var vm = new LuaValueDynamicViewModel(table, context);
		var notified = new List<string?>();
		vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

		vm.SetDynamicMember("value", 10d);

		Assert.Equal(10d, table.Get("value").TryToNumber()!.Value);
		Assert.Contains("value", changed);
		Assert.Single(changed);
		Assert.Contains("value", notified);
	}

	[Fact]
	public void LuaTable_NotifyDoesNotInvokeChangedCallback()
	{
		var state = new LuaState();
		var context = state.CreateContext();
		var table = new LuaTable();
		var changed = 0;
		table.Set(LuaValueDynamicViewModel.ChangedKey, new LuaCallbackFunction((_, _) =>
		{
			changed++;
			return LuaTuple.Empty;
		}));

		var vm = new LuaValueDynamicViewModel(table, context);
		var notifyFunction = Assert.IsAssignableFrom<LuaFunction>(table.Get(LuaValueDynamicViewModel.NotifyKey));
		notifyFunction.Invoke(context, table, new LuaString("value"));

		Assert.Equal(0, changed);
	}

	[Fact]
	public async Task LuaFunction_IsExposedAsAsyncCommand()
	{
		var state = new LuaState();
		var context = state.CreateContext();
		var table = new LuaTable();
		var invoked = new List<LuaValue>();
		table.Set("go", new LuaCallbackFunction((_, args) =>
		{
			invoked.AddRange(args);
			return LuaTuple.Empty;
		}));

		var vm = new LuaValueDynamicViewModel(table, context);
		var command = Assert.IsAssignableFrom<IAsyncRelayCommand<object?>>(vm.GetDynamicMember("go"));
		Assert.Same(command, vm.GetDynamicMember("go"));

		// No parameter -> data:go() -> go(self).
		await command.ExecuteAsync(null);
		Assert.Single(invoked);
		Assert.Same(table, invoked[0]);

		// Parameter -> data:go(payload) -> go(self, payload).
		invoked.Clear();
		await command.ExecuteAsync("payload");
		Assert.Equal(2, invoked.Count);
		Assert.Same(table, invoked[0]);
		Assert.Equal("payload", invoked[1].TryToString());
	}

	[Fact]
	public async Task LuaFunction_CombinedHard()
	{
		var context = new LuaState().CreateContext();

		var log = new List<string>();
		context.Globals["log"] = new LuaCallbackFunction((_, args) =>
		{
			log.Add(args[0].TryToString()!);
			return LuaTuple.Empty;
		});
		context.Globals["to_vm"] = new LuaCallbackFunction((ctx, args) =>
		{
			var table = (LuaTable)args[0];
			return new LuaTuple(new LuaUserData(new LuaValueDynamicViewModel(table, ctx)));
		});

		var result = context.Execute("""
			global data = {
				value = 42,
				inner = {
					name = "demo",
					lorem = "ipsum"
				},
				array = { "a", "b", "c" },
				click = function(self, payload)
					log("click:" .. payload)
				end,
				__changed = function(self, name, old, new)
					log("changed:" .. name .. ":" .. (old == nil and "nil" or old) .. ":" .. (new == nil and "nil" or new))
				end
			}

			global vm = to_vm(data)
			return vm, data
			""");

		var userData = Assert.IsType<LuaUserData>(result[0]);
		var table = Assert.IsType<LuaTable>(result[1]);
		var vm = Assert.IsType<LuaValueDynamicViewModel>(userData.Target);

		// Member projection, built from a table authored in Lua.
		Assert.Equal(42d, vm.GetDynamicMember("value"));
		var inner = Assert.IsType<LuaValueDynamicViewModel>(vm.GetDynamicMember("inner"));
		var array = Assert.IsType<LuaValueDynamicViewModel>(vm.GetDynamicMember("array"));
		Assert.Equal("demo", inner.GetDynamicMember("name"));
		Assert.Equal("a", array.GetDynamicMember("0"));
		Assert.Equal("c", array.GetDynamicMember("2"));

		// Commands use method semantics: click(self, payload).
		var command = Assert.IsAssignableFrom<IAsyncRelayCommand<object?>>(vm.GetDynamicMember("click"));
		await command.ExecuteAsync("hit");
		Assert.Contains("click:hit", log);

		// Writing a value fires __changed(self, name, old, new).
		vm.SetDynamicMember("value", 100d);
		Assert.Contains("changed:value:42:100", log);

		// Setting value from Lua
		log.Clear();

		// Does not fire __changed because value is not changed yet (100 == 100).
		context.Execute("""
			data:set("value", 100)
			""");
		Assert.Empty(log);

		context.Execute("""
			data:set("value", 200)
			data:notify("value")
			data:set("new_value", "something")
			""");
		Assert.Equal(["changed:value:100:200", "changed:new_value:nil:something"], log);
	}
}
