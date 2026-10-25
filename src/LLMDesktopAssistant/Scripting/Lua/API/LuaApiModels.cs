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
			    - provider_name: string - provider name (e.g. "OpenAI")
			    - name: string - model name (e.g. "gpt-4o")
			    - display_name: string - model display name (e.g. "GPT 4o")
			    - full_name: string - full model identifier (e.g. "OpenAI$gpt-4o")

			EXAMPLES:

			  -- List all models
			  local models = dass.models.list()
			  for _, m in ipairs(models) do
			    print(m.full_name .. " (" .. m.provider_name .. ")")
			  end

			NOTES:
			  - Models are sourced from all configured providers.
			""";

		private readonly IModelManager _modelManager;

		public LuaApiModels(IModelManager modelManager)
		{
			_modelManager = modelManager;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["list"] = new LuaCallbackFunction(ListModels);
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
				entry["provider_name"] = new LuaString(model.Provider.Name);
				entry["name"] = new LuaString(model.Descriptor.Name);
				entry["display_name"] = new LuaString(model.Descriptor.DisplayName);
				entry["full_name"] = new LuaString(model.FullName);
				resultTable.Set(i++, entry);
			}

			return new LuaTuple(resultTable);
		}
	}
}
