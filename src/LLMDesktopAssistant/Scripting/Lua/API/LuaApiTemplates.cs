using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLTSharp;
using LLTSharp.DataAccessors;
using LLTSharp.Metadata;
using LLTSharp.Metadata.Types;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiTemplates : LuaApiBase
	{
		public override string? Namespace => "dass.templates";

		public override string? Manuals => """
			--- dass.templates - template rendering and inspection API

			Provides access to the template library for rendering and listing templates.
			Templates are identified by ID and can be filtered by metadata (language, model, etc.).

			FUNCTIONS:
			
			--- dass.templates.render(filters, [context])
			  Renders a template from the template library.
			  Parameters:
			    - filters: string or table - Specifies which template to render.
			      If string: treated as a template ID (e.g. "system_prompt").
			      If table: supports the following keys:
			        - Numeric key: Template ID.
			        - "id": string - Explicit template ID.
			        - "lang": string - Language code (e.g. "en_US", "ru_RU", "fr_FR", etc.).
			        - "model": string - Target model name.
			        - "model_family": string - Target model family.
			        - "version": string - Version string (e.g. "1.0.0").
			        - Any other string key: treated as additional custom metadata.
			    - context: any (optional) - Data context for template variables.
			      Accepts nil, boolean, number, string, or table.
			      Tables with sequential integer keys become arrays.
			      Tables with string/non-sequential keys become objects/dictionaries.
			  Returns:
			    - string - for text templates.
			    - table - for message templates:
			      Returns an array of { role: string, content: string } tables.

			--- dass.templates.list([filters])
			  Lists all templates registered in the template library with their metadata.
			  Parameters:
			    - filters: table (optional) - Filter criteria. Supports:
			      - Numeric keys (1, 2, 3...): template IDs to filter by.
			      - "id": string - Template ID to filter by.
			      - "type": string - Template type: "text" or "messages".
			      - "lang": string - Language code.
			      - "model": string - Target model name.
			      - "model_family": string - Target model family.
			      - "version": string - Version string.
			      - Any other string key: custom metadata to filter by.
			  Returns:
			    - table - Array of template descriptor tables. Each entry contains:
			      - id: string - Template identifier.
			      - type: string - "text" or "messages".
			      - lang: string (optional) - Language code.
			      - model: string (optional) - Target model.
			      - model_family: string (optional) - Target model family.
			      - version: string (optional) - Version.
			      - ... plus any custom metadata keys flattened directly.
			
			NOTES:
			  - In dass.templates.render(), templates are matched by intersecting all provided
			    metadata criteria. Each metadata filter narrows down the candidate set.
			  - If multiple templates match all filters, the first one is returned.

			EXAMPLES:
			
			  -- Import a new template from a LLT string
			  local count = dass.templates.import([[
			    @template hello { Hello, @name! }
			  ]])
			  print("Imported " .. count .. " templates")

			  -- Render by template ID (string filter)
			  local prompt = dass.templates.render("system_prompt")
			  print(prompt)

			  -- Render with language filter
			  local prompt = dass.templates.render({
			    "system_prompt",
			    lang = "ru_RU"
			  })
			  print(prompt)

			  -- Render with context data
			  local prompt = dass.templates.render("greeting", {
			    name = "Alice",
			    age = 30,
			    tags = {"admin", "user"}
			  })

			  -- Render a messages template (returns array of messages)
			  local messages = dass.templates.render("chat_messages", {
			    topic = "Hello World"
			  })
			  for _, msg in ipairs(messages) do
			    print(msg.role, msg.content)
			  end

			  -- List all templates
			  local all = dass.templates.list()
			  for _, t in ipairs(all) do
			    print(t.id, t.type, t.lang)
			  end

			  -- List templates by language
			  local ru = dass.templates.list({ lang = "ru_RU" })

			  -- List text templates only
			  local texts = dass.templates.list({ type = "text" })

			  -- List by custom metadata
			  local experimental = dass.templates.list({ my_custom_tag = "experimental" })
			""";

		private readonly IChatTemplateImporter _chatTemplateImporter;
		private readonly IEnumerable<IPromptTemplatePlugin> _promptTemplatePlugins;

		public LuaApiTemplates(IChatTemplateImporter chatTemplateImporter, IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins)
		{
			_chatTemplateImporter = chatTemplateImporter;
			_promptTemplatePlugins = promptTemplatePlugins;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["render"] = new LuaCallbackFunction(Render);
			ns["list"] = new LuaCallbackFunction(List);
			return null;
		}

		private LuaTuple Render(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dass.templates.render(filters, [context]): at least 1 argument expected.");

			var filters = args[0];
			if (filters is not LuaString && filters is not LuaTable)
				throw new LuaRuntimeException("dass.templates.render(): first argument must be a string or a table.");

			TemplateDataAccessor context = TemplateNullAccessor.Instance;
			if (args.Length > 1)
				context = StructuredLuaConverter.LuaValueToLLTSharp(args[1]);

			try
			{
				ITemplate template;
				var functions = new TemplateFunctionSet(_promptTemplatePlugins.SelectMany(p => p.GetTemplateFunctions()));

				if (filters is LuaString filterStr)
				{
					template = _chatTemplateImporter.Library.RetrieveAll(filterStr.Value).Last();
				}
				else
				{
					var filtersTable = (LuaTable)filters;
					string? id = null;
					var metadatas = new List<IMetadata>();

					foreach (var kvp in filtersTable.Entries)
					{
						if (kvp.Key is LuaNumber)
						{
							if (kvp.Value is not LuaString)
								throw new LuaRuntimeException("dass.templates.render(): filter table values for number keys must be strings.");

							id = ((LuaString)kvp.Value).Value;
						}
						else if (kvp.Key is LuaString keyStr)
						{
							if (kvp.Value is not LuaString valStr)
								throw new LuaRuntimeException("dass.templates.render(): filter table values for string keys must be strings.");

							var key = keyStr.Value.ToLowerInvariant();
							var value = valStr.Value;
							switch (key)
							{
								case "id":
									id = value;
									break;
								case "lang":
									metadatas.Add(new LanguageMetadata(value));
									break;
								case "model":
									metadatas.Add(new TargetModelMetadata(value));
									break;
								case "model_family":
									metadatas.Add(new TargetModelFamilyMetadata(value));
									break;
								case "version":
									metadatas.Add(new VersionMetadata(Version.Parse(value)));
									break;
								default:
									metadatas.Add(new AdditionalMetadata(key, value));
									break;
							}
						}
						else
						{
							throw new LuaRuntimeException("dass.templates.render(): filter table keys must be numbers or strings.");
						}
					}

					if (id != null)
						template = _chatTemplateImporter.Library.RetrieveAll(id, metadatas.ToArray()).Last();
					else
						template = _chatTemplateImporter.Library.RetrieveAll(metadatas.ToArray()).Last();
				}

				if (template is ITextTemplate textTemplate)
				{
					return new LuaTuple(new LuaString(textTemplate.Render(context, functions)));
				}
				else if (template is IMessagesTemplate messagesTemplate)
				{
					var messages = messagesTemplate.Render(context, functions);
					var messagesTable = new LuaTable();
					foreach (var m in messages)
					{
						var msgTable = new LuaTable();
						msgTable["role"] = new LuaString(m.Role.ToString());
						msgTable["content"] = new LuaString(m.Content);
						messagesTable.Append(msgTable);
					}
					return new LuaTuple(messagesTable);
				}

				throw new LuaRuntimeException($"dass.templates.render(): not supported template type: {template?.GetType().FullName}.");
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"dass.templates.render(): {ex.Message}");
			}
		}

		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			// Parse optional filter
			Dictionary<string, string>? stringFilters = null;
			string? typeFilter = null;
			string? idFilter = null;

			if (args.Length > 0 && args[0] is LuaTable filterTable)
			{
				stringFilters = new Dictionary<string, string>();
				foreach (var kvp in filterTable.Entries)
				{
					if (kvp.Key is LuaNumber)
					{
						if (kvp.Value is not LuaString)
							throw new LuaRuntimeException("dass.templates.list(): filter table values for number keys must be strings.");
						idFilter = ((LuaString)kvp.Value).Value;
					}
					else if (kvp.Key is LuaString keyStr)
					{
						if (kvp.Value is not LuaString valStr)
							throw new LuaRuntimeException("dass.templates.list(): filter table values for string keys must be strings.");

						var key = keyStr.Value.ToLowerInvariant();
						var value = valStr.Value;

						if (key == "id")
							idFilter = value;
						else if (key == "type")
							typeFilter = value;
						else
							stringFilters[key] = value;
					}
					else
					{
						throw new LuaRuntimeException("dass.templates.list(): filter table keys must be numbers or strings.");
					}
				}
			}

			var result = new LuaTable();
			int index = 1;

			foreach (var template in _chatTemplateImporter.Library)
			{
				if (typeFilter != null)
				{
					string type = template is ITextTemplate ? "text"
						: template is IMessagesTemplate ? "messages"
						: "unknown";
					if (!string.Equals(type, typeFilter, StringComparison.OrdinalIgnoreCase))
						continue;
				}

				if (idFilter != null)
				{
					var tmplIdMeta = template.GetMetadata<TemplateIdentifierMetadata>();
					if (tmplIdMeta == null || !string.Equals(tmplIdMeta.Identifier, idFilter, StringComparison.OrdinalIgnoreCase))
						continue;
				}

				if (stringFilters != null && stringFilters.Count > 0)
				{
					if (!TemplateMatchesFilters(template, stringFilters))
						continue;
				}

				var entry = new LuaTable();

				var idMeta = template.GetMetadata<TemplateIdentifierMetadata>();
				if (idMeta != null)
					entry["id"] = new LuaString(idMeta.Identifier);

				if (template is ITextTemplate)
					entry["type"] = new LuaString("text");
				else if (template is IMessagesTemplate)
					entry["type"] = new LuaString("messages");

				var langMeta = template.GetMetadata<LanguageMetadata>();
				if (langMeta != null)
					entry["lang"] = new LuaString(langMeta.LanguageCode.ToString());

				var modelMeta = template.GetMetadata<TargetModelMetadata>();
				if (modelMeta != null)
					entry["model"] = new LuaString(modelMeta.ModelName);

				var familyMeta = template.GetMetadata<TargetModelFamilyMetadata>();
				if (familyMeta != null)
					entry["model_family"] = new LuaString(familyMeta.FamilyName);

				var versionMeta = template.GetMetadata<VersionMetadata>();
				if (versionMeta != null)
					entry["version"] = new LuaString(versionMeta.Version.ToString());

				foreach (var additionalMeta in template.GetAllMetadata<AdditionalMetadata>())
				{
					if (entry.ContainsKey(new LuaString(additionalMeta.Key)))
						continue;

					entry[additionalMeta.Key] = LuaValueConverter.ToLuaValue(additionalMeta.Value);
				}

				result[index++] = entry;
			}

			return new LuaTuple(result);
		}

		private static bool TemplateMatchesFilters(ITemplate template, Dictionary<string, string> filters)
		{
			foreach (var filter in filters)
			{
				var key = filter.Key;
				var value = filter.Value;

				switch (key)
				{
					case "lang":
						{
							var lang = template.GetMetadata<LanguageMetadata>();
							if (lang == null || !string.Equals(lang.LanguageCode.ToString(), value, StringComparison.OrdinalIgnoreCase))
								return false;
							break;
						}

					case "model":
						{
							var model = template.GetMetadata<TargetModelMetadata>();
							if (model == null || !string.Equals(model.ModelName, value, StringComparison.OrdinalIgnoreCase))
								return false;
							break;
						}

					case "model_family":
						{
							var family = template.GetMetadata<TargetModelFamilyMetadata>();
							if (family == null || !string.Equals(family.FamilyName, value, StringComparison.OrdinalIgnoreCase))
								return false;
							break;
						}

					case "version":
						{
							var version = template.GetMetadata<VersionMetadata>();
							if (version == null || !string.Equals(version.Version.ToString(), value, StringComparison.OrdinalIgnoreCase))
								return false;
							break;
						}

					default:
						{
							var customMetas = template.GetAllMetadata<AdditionalMetadata>();
							bool found = false;
							foreach (var customMeta in customMetas)
							{
								if (customMeta.Key == key)
								{
									var metaVal = customMeta.Value;
									if (string.Equals(metaVal?.ToString(), value, StringComparison.OrdinalIgnoreCase))
									{
										found = true;
										break;
									}
								}
							}
							if (!found)
								return false;
							break;
						}
				}
			}
			return true;
		}
	}
}
