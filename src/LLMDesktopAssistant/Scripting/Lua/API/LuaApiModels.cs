using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Providers;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: false)]
	public class LuaApiModels : LuaApiBase
	{
		public override string? Namespace => "dass.models";

		public override string? Manuals => """
			--- dass.models - model listing API

			Provides access to the list of available LLM models.

			FUNCTIONS:

			--- dass.models.list()
			  Returns a table with detailed information about all available models.

			  Returns: table - array of model info tables. Each entry contains:
			    - name: string - model name (e.g. "gpt-4o")
			    - display_name: string - model display name (e.g. "GPT 4o")
			    - provider_name: string - provider name (e.g. "OpenAI")
			    - full_name: string - full model identifier (e.g. "OpenAI$gpt-4o")
			
			--- dass.models.modifiers()
			  Returns a table with information about all available model modifiers.
			
			  Returns: table - array of modifier info tables. Each entry contains:
			    - name: string - modifier name, used to identify it
			    - hint: string or nil - modifier display name in the UI

			EXAMPLES:

			  -- List all models
			  local models = dass.models.list()
			  for _, m in ipairs(models) do
			    print(m.full_name .. " (" .. m.provider_name .. ")")
			  end

			NOTES:
			  - Models are sourced from all configured providers.
			  - Modifiers can be used to parametrize model requests, e.g. change a
			    model's temperature. To apply modifier, just type it to the model full
			    name: "OpenAI$gpt-4o$40KTokens", where "40KTokens" is a modifier name,
			    usually defined by user.
			""";

		private readonly IModelManager _modelManager;

		public LuaApiModels(IModelManager modelManager)
		{
			_modelManager = modelManager;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["list"] = new LuaCallbackFunction(ListModels);
			ns["modifiers"] = new LuaCallbackFunction(ListModifiers);
			return null;
		}

		private LuaTuple ListModels(LuaCallingContext ctx, LuaValue[] args)
		{
			var models = _modelManager.ListModels();
			var resultTable = new LuaTable();

			int i = 1;
			foreach (var model in models)
			{
				var entry = new LuaTable();
				entry["name"] = new LuaString(model.Descriptor.Name);
				entry["display_name"] = new LuaString(model.Descriptor.DisplayName);
				entry["provider_name"] = new LuaString(model.Provider.Name);
				entry["full_name"] = new LuaString(model.FullName);
				resultTable.Set(i++, entry);
			}

			return new LuaTuple(resultTable);
		}

		private LuaTuple ListModifiers(LuaCallingContext ctx, LuaValue[] args)
		{
			var modifiers = _modelManager.ListModifiers();
			var resultTable = new LuaTable();

			int i = 1;
			foreach (var modifier in modifiers)
			{
				var entry = new LuaTable();
				entry["name"] = new LuaString(modifier.Name);
				entry["hint"] = string.IsNullOrEmpty(modifier.Hint) ? LuaNil.Instance : new LuaString(modifier.Hint);
				resultTable.Set(i++, entry);
			}

			return new LuaTuple(resultTable);
		}
	}
}
