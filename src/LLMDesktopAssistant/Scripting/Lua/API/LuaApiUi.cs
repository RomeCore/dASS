using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.MVVM.Dynamic;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for creating dynamic UI controls: <c>dass.ui.*</c>.
	/// A created control is a pure descriptor (<c>uicontrol</c> UserData) that can be appended to
	/// a running tool's result via <c>dass.tool.result.append_data()</c>.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiUi : LuaApiBase
	{
		public override string? Namespace => "dass.ui";

		public override string? Manuals => """
			--- dass.ui — dynamic UI creation API

			Creates dynamic Avalonia UI controls from AXAML markup bound to a Lua table (a dynamic
			view model). The created control is a UserData descriptor; it renders only after it is
			appended to a running tool's result with dass.tool.result.append_data().

			FUNCTIONS:

			--- dass.ui.create_control(xaml, view_model) -> uicontrol
			  Parses nothing yet: it packs the AXAML source and the view model into a uicontrol handle.
			  The control is rendered later, inside the tool call UI, on the UI thread.

			  Parameters:
			    - xaml: string — AXAML markup (reflection bindings, i.e. `{Binding key}`).
			      Do not use compiled bindings or x:DataType.
			    - view_model: table or nil — the dynamic view model:
			        * table — a Lua table exposed as a dynamic view model. String keys become
			          bindable members, function values become commands, nested tables become nested
			          view models. The injected helpers `data:set(name, value)` and `data:notify(name)`
			          and the optional `__changed(self, name, old, new)` callback drive reactivity.
			        * nil — an empty view model (useful for purely static markup).

			  Returns: uicontrol (UserData) — pass it to dass.tool.result.append_data().

			NOTES:
			  - The handle is reusable: the same uicontrol may be appended multiple times.
			  - Commands (Lua functions) survive only for the live session; after a chat reload the
			    UI is restored from persisted data, but functions are lost.

			EXAMPLES:

			  local vm = {
			    title = "Weather",
			    temperature = "22°C",
			    refresh = function(self)
			      -- ... update vm ...
			      vm:set("temperature", "23°C")
			    end
			  }
			  local ui = dass.ui.create_control([[
			    <StackPanel xmlns="https://github.com/avaloniaui" Spacing="4">
			      <TextBlock Text="{Binding title}" FontWeight="Bold"/>
			      <TextBlock Text="{Binding temperature}"/>
			      <Button Content="Refresh" Command="{Binding refresh}"/>
			    </StackPanel>
			  ]], vm)

			  dass.tool.result.append_data(ui)
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["create_control"] = new LuaCallbackFunction(CreateControl);
			return null;
		}

		private static LuaTuple CreateControl(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString xaml || string.IsNullOrWhiteSpace(xaml.Value))
				throw new LuaRuntimeException("dass.ui.create_control(xaml, view_model): first argument must be a non-empty string (AXAML).");

			DynamicViewModel viewModel;
			var viewModelArg = args.Length > 1 ? args[1] : LuaNil.Instance;
			switch (viewModelArg)
			{
				case LuaNil:
					viewModel = new DictionaryDynamicViewModel();
					break;
				case LuaTable table:
					viewModel = new LuaValueDynamicViewModel(table, ctx);
					break;
				default:
					throw new LuaRuntimeException("dass.ui.create_control(xaml, view_model): second argument must be a Lua table or nil.");
			}

			var control = new LuaUiControl(xaml.Value, viewModel);
			return new LuaTuple(new LuaUserData(control, "uicontrol"));
		}
	}
}
