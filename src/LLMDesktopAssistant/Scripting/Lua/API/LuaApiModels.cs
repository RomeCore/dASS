using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Providers;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: false)]
	public class LuaApiModels : LuaApiBase
	{
		public override string? Namespace => "dass.model";

		public override string? Manuals => """
			--- dass.model - LLM names API

			Provides access to the list of available large language models (LLM)
			within config and manipulations with their names.
			In dASS, models are identified using this format:
			ProviderName$ModelName[$Modifier1,Modifier2,...]

			Examples:
			DeepSeek$deepseek-flash
			OpenAI$gpt-6-sol$MaxReasoning

			Note that provider name and modifiers are USER-DEFINED names in the dASS's configuration,
			but model name is the real names that are recognized at the provider's side. So user
			can name their provider like "MyOllamaRemote1337" and configure only the "llama3.1" in
			the dASS, so dass.model.list() will return only "MyOllamaRemote1337$llama3.1", BUT
			"MyOllamaRemote1337$lfm3-omni" will also work (but only if "lfm3-omni" is really
			installed at the target provider), even if it is not listed in the configuration.
			Conclusion: provider name MUST be configured (and modifiers too), but model name MUST
			exist provider-side, but not required to be dASS-side configured.

			FUNCTIONS:

			--- dass.model.list()
			  Returns a table with detailed information about all available models.

			  Returns: table - array of model info tables. Each entry contains:
			    - name: string - model name (e.g. "gpt-4o")
			    - display_name: string - model display name (e.g. "GPT 4o")
			    - provider_name: string - provider name (e.g. "OpenAI")
			    - full_name: string - full model identifier (e.g. "OpenAI$gpt-4o")
			
			--- dass.model.modifiers()
			  Returns a table with information about all available model modifiers.
			
			  Returns: table - array of modifier info tables. Each entry contains:
			    - name: string - modifier name, used to identify it
			    - hint: string or nil - modifier display name in the UI
			
			--- dass.model.parse(full_name)
			  Parses a model's full name into a provider name, model name and modifiers
			
			  Returns: table - result of model full name parsing:
			    - provider: string - provider name
			    - model: string - model name
			    - modifiers: table or nil - modifiers as an array
			
			--- dass.model.compose(provider, model, [modifiers...])
			  Composes a full model name from provider, model and optional modifiers
			
			  Returns: string
			
			EXAMPLES:

			  -- List all models
			  local models = dass.model.list()
			  for _, m in ipairs(models) do
			    print(m.full_name .. " (" .. m.provider_name .. ")")
			  end

			  -- Parse model name
			  local parsed = dass.model.parse("DeepSeek$deepseek-flash$NoReasoning,StrictTools")
			  print(parsed.provider)                       -- DeepSeek
			  print(parsed.model)                          -- deepseek-flash
			  print(table.concat(parsed.modifiers, " + ")) -- NoReasoning + StrictTools

			  -- Compose model name
			  local composed = dass.model.compose("Anthropic", "claude-fable-5-2", "Max8kTokens")
			  print(composed) -- Anthropic$claude-fable-5-2$Max8kTokens

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
			ns["parse"] = new LuaCallbackFunction(Parse);
			ns["compose"] = new LuaCallbackFunction(Compose);
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

		private LuaTuple Parse(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dass.model.parse(full_name): at least 1 argument expected");

			if (args[0] is not LuaString fullName)
				throw new LuaRuntimeException("dass.model.parse(full_name): full_name must be a string");

			if (!ModelReference.TryParse(fullName.Value, out var model))
				throw new LuaRuntimeException($"dass.model.parse(full_name): full_name ({fullName.Value}) has invalid format");

			LuaTable? modifiers = null;

			if (model.Modifiers is { Count :> 0 } parsedModifiers)
			{
				modifiers = new LuaTable();
				foreach (var modifier in parsedModifiers)
					modifiers.Append(new LuaString(modifier));
			}

			return new LuaTuple(new LuaTable
			{
				["provider"] = new LuaString(model.Provider),
				["model"] = new LuaString(model.ModelId),
				["modifiers"] = (LuaValue?)modifiers ?? LuaNil.Instance
			});
		}

		private LuaTuple Compose(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("dass.model.compose(provider, model, [modifiers...]): at least 2 arguments expected");

			if (args[0] is not LuaString provider || provider.Value.Contains('$'))
				throw new LuaRuntimeException("dass.model.compose(provider, model, [modifiers...]): provider must be a string that not contains a '$'");

			if (args[1] is not LuaString model || model.Value.Contains('$'))
				throw new LuaRuntimeException("dass.model.compose(provider, model, [modifiers...]): model must be a string that not contains a '$'");

			List<string> modifiers = [];
			for (int i = 2; i < args.Length; i++)
				if (args[i] is not LuaString modifier || modifier.Value.Contains('$') || modifier.Value.Contains(','))
					throw new LuaRuntimeException("dass.model.compose(provider, model, [modifiers...]): all modifiers must be strings without '$' or ','");
				else if (!string.IsNullOrEmpty(modifier.Value))
					modifiers.Add(modifier.Value);

			return new LuaTuple(new LuaString(new ModelReference(provider.Value, model.Value, modifiers).ToString()));
		}
	}
}
