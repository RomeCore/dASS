using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations
{
	/// <summary>
	/// The addon tools ('addon-search', 'addon-list_available' and 'addon-info') that search, list and
	/// inspect the addons available to the calling agent (skills, sub-agents, tools and Lua scripts)
	/// through the per-kind <see cref="IAddonAgenticSearchProvider"/> instances.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Hidden addons are included: they are intentionally excluded from the agent prompt and toolset
	/// (see <c>ChatExecutionService</c> and <c>ChatPromptBuilder</c>), exactly as
	/// <c>AddonChangedBase.Hidden</c> documents it, so the addon tools are the only way to discover them.
	/// Disabled addons are not returned by the collectors and therefore are not searchable.
	/// </para>
	/// <para>
	/// Every provider renders its own body (tools render the argument schema, skills render the loading
	/// hint, etc.), while the group header ('Title — UsageHint') is rendered by the tools themselves.
	/// </para>
	/// <para>
	/// The valid 'kinds' filter values are the <see cref="IAddonTypeDescriptor.Type"/> strings of the
	/// descriptors that have a provider: nothing is hardcoded, the argument schemas and the error
	/// messages stay in sync with the registered addon types automatically. A new addon type with a
	/// provider becomes available to all three tools without touching this module.
	/// </para>
	/// </remarks>
	[ToolModule]
	public class AddonToolModule : ToolModule
	{
		private const int DefaultMaxResults = 5;
		private const int MaxResultsLimit = 25;

		private readonly IAgentManagementService _agentManager;
		private readonly IReadOnlyList<IAddonAgenticSearchProvider> _providers;
		private readonly IReadOnlyDictionary<string, AddonKind> _kindByType;
		private readonly IReadOnlyList<string> _searchableTypes;
		private readonly AddonKind _allSearchableKinds;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonToolModule"/> class.
		/// </summary>
		public AddonToolModule(IAgentManagementService agentManager,
			IEnumerable<IAddonAgenticSearchProvider> providers,
			IEnumerable<IAddonTypeDescriptor> addonTypeDescriptors)
		{
			_agentManager = agentManager;
			_providers = [.. providers.OrderBy(provider => (int)provider.Kind)];

			var kindByType = new Dictionary<string, AddonKind>(StringComparer.OrdinalIgnoreCase);
			var searchableTypes = new List<string>();
			var allSearchableKinds = AddonKind.None;

			// The filter values are the addon type strings (for example, 'skills', 'agents' or
			// 'scripts/lua') of the descriptors that have a provider — the same source
			// the addon system itself uses.
			foreach (var provider in _providers)
			{
				allSearchableKinds |= provider.Kind;

				foreach (var descriptor in addonTypeDescriptors
					.Where(descriptor => descriptor.Kind == provider.Kind)
					.OrderBy(descriptor => descriptor.Type, StringComparer.OrdinalIgnoreCase))
				{
					if (kindByType.TryAdd(NormalizeType(descriptor.Type), descriptor.Kind))
						searchableTypes.Add(descriptor.Type);
				}
			}

			_kindByType = kindByType;
			_searchableTypes = [.. searchableTypes];
			_allSearchableKinds = allSearchableKinds;

			AddTool(new ToolInitializationInfo
			{
				Executor = SearchAddons,
				IsFixed = true,
				Name = "addon-search",
				Description = "Searches the addons available to you (skills, sub-agents, tools and Lua scripts) " +
					"by a free-form query over their names, tags and descriptions. Hidden addons are included.",
				NameKey = Locale.GetKey("tool.name.addon-search"),
				DescriptionKey = Locale.GetKey("tool.description.addon-search"),
				CategoryKey = Locale.GetKey("tool.category.addons"),
				DefaultExpectedBehaviour = ToolBehaviour.None,
				ModifyArgumentSchema = ModifyArgumentSchema
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ListAvailableAddons,
				IsFixed = true,
				Name = "addon-list_available",
				Description = "Lists the names of every addon available to you (skills, sub-agents, tools and " +
					"Lua scripts), grouped by kind. Hidden addons are included.",
				NameKey = Locale.GetKey("tool.name.addon-list_available"),
				DescriptionKey = Locale.GetKey("tool.description.addon-list_available"),
				CategoryKey = Locale.GetKey("tool.category.addons"),
				DefaultExpectedBehaviour = ToolBehaviour.None,
				ModifyArgumentSchema = ModifyArgumentSchema
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GetAddonInfo,
				IsFixed = true,
				Name = "addon-info",
				Description = "Shows the detailed information about a single addon available to you, " +
					"looked up by its exact name. Hidden addons are included.",
				NameKey = Locale.GetKey("tool.name.addon-info"),
				DescriptionKey = Locale.GetKey("tool.description.addon-info"),
				CategoryKey = Locale.GetKey("tool.category.addons"),
				DefaultExpectedBehaviour = ToolBehaviour.None
			});
		}

		private ReactiveToolResult SearchAddons(
			[Description("The free-form search query, for example 'code review' or 'postgres migrations'.")]
			string query,
			ToolExecutionContext ctx,
			[Description("Optional filters by addon type. Omit the parameter to use all available kinds.")]
			string[]? kinds = null,
			[Description("The maximum number of matches per addon kind.")]
			[Range(1, MaxResultsLimit)]
			int maxResults = DefaultMaxResults,
			[Description("Include the detailed representation of every match (tags, source pack, paths, tool argument schemas).")]
			bool detailed = false)
		{
			if (string.IsNullOrWhiteSpace(query))
				return CreateError("The search query must not be empty.");

			var kindsError = TryParseKinds(kinds, out var kindsFilter);
			if (kindsError is not null)
				return CreateError(kindsError);

			maxResults = Math.Clamp(maxResults <= 0 ? DefaultMaxResults : maxResults, 1, MaxResultsLimit);

			var agent = _agentManager.GetAgentDescriptor(ctx.Message.SenderAgentId);

			var builder = new StringBuilder();
			foreach (var provider in _providers)
			{
				if ((kindsFilter & provider.Kind) == 0)
					continue;

				var body = provider.Search(query, agent, maxResults, detailed);
				if (string.IsNullOrWhiteSpace(body))
					continue;

				if (builder.Length > 0)
					builder.AppendLine();

				builder.Append("**").Append(provider.Title).Append("** — ")
					.Append(provider.UsageHint).AppendLine(":");
				builder.AppendLine(body);
			}

			if (builder.Length == 0)
			{
				return new ReactiveToolResult
				{
					StatusIcon = MaterialIconKind.Search,
					StatusTitle = $"**{query}**",
					ResultContent = $"No addons matched \"{query}\". Try different or fewer terms" +
						(kindsFilter == _allSearchableKinds ? "." : " or omit the 'kinds' filter."),
					UseMarkdown = true
				}.CompleteWithSuccess();
			}

			return new ReactiveToolResult
			{
				StatusIcon = MaterialIconKind.Search,
				StatusTitle = $"**{query}**",
				ResultContent = builder.ToString(),
				UseMarkdown = true
			}.CompleteWithSuccess();
		}

		private ReactiveToolResult ListAvailableAddons(
			ToolExecutionContext ctx,
			[Description("Optional filters by addon type. Omit the parameter to use all available kinds.")]
			string[]? kinds = null)
		{
			var kindsError = TryParseKinds(kinds, out var kindsFilter);
			if (kindsError is not null)
				return CreateError(kindsError, MaterialIconKind.FormatListBulleted);

			var agent = _agentManager.GetAgentDescriptor(ctx.Message.SenderAgentId);

			var builder = new StringBuilder();
			foreach (var provider in _providers)
			{
				if ((kindsFilter & provider.Kind) == 0)
					continue;

				var body = provider.List(agent);
				if (string.IsNullOrWhiteSpace(body))
					continue;

				if (builder.Length > 0)
					builder.AppendLine();

				builder.Append("**").Append(provider.Title).Append("** — ")
					.Append(provider.UsageHint).AppendLine(":");
				builder.AppendLine(body);
			}

			if (builder.Length == 0)
			{
				return new ReactiveToolResult
				{
					StatusIcon = MaterialIconKind.FormatListBulleted,
					ResultContent = kindsFilter == _allSearchableKinds
						? "No addons are available to you."
						: "No addons are available for the specified kinds.",
					UseMarkdown = true
				}.CompleteWithSuccess();
			}

			return new ReactiveToolResult
			{
				StatusIcon = MaterialIconKind.FormatListBulleted,
				ResultContent = builder.ToString(),
				UseMarkdown = true
			}.CompleteWithSuccess();
		}

		private ReactiveToolResult GetAddonInfo(
			[Description("The exact addon name, it can be a tool name or skill name.")]
			string name,
			ToolExecutionContext ctx)
		{
			if (string.IsNullOrWhiteSpace(name))
				return CreateError("The addon name must not be empty.", MaterialIconKind.Information);

			name = name.Trim();
			var agent = _agentManager.GetAgentDescriptor(ctx.Message.SenderAgentId);

			var builder = new StringBuilder();
			foreach (var provider in _providers)
			{
				var body = provider.Info(name, agent);
				if (string.IsNullOrWhiteSpace(body))
					continue;

				if (builder.Length > 0)
					builder.AppendLine();
				builder.Append("**").Append(provider.Title).Append("** — ")
					.Append(provider.UsageHint).AppendLine(":");
				builder.AppendLine(body);
			}

			if (builder.Length == 0)
			{
				return new ReactiveToolResult
				{
					StatusIcon = MaterialIconKind.Information,
					StatusTitle = $"**{name}**",
					ResultContent = $"No addon with the exact name \"{name}\" is available to you. " +
						"Use `addon-list_available` to see the exact available names.",
					UseMarkdown = true
				}.CompleteWithError();
			}

			return new ReactiveToolResult
			{
				StatusIcon = MaterialIconKind.Information,
				StatusTitle = $"**{name}**",
				ResultContent = builder.ToString(),
				UseMarkdown = true
			}.CompleteWithSuccess();
		}

		/// <summary>
		/// Fills the argument schema of the tools with the actual addon type strings accepted by the
		/// 'kinds' parameter: the values are the <see cref="IAddonTypeDescriptor.Type"/> strings of the
		/// descriptors that have a provider, so the model always sees the real list.
		/// </summary>
		private void ModifyArgumentSchema(JsonObject schema)
		{
			if (schema["properties"] is not JsonObject properties ||
				properties["kinds"] is not JsonObject kindsProperty)
				return;

			var values = new JsonArray();
			foreach (var type in _searchableTypes)
				values.Add(type);

			if (kindsProperty["items"] is JsonObject items)
				items["enum"] = values;
			else
				kindsProperty["items"] = new JsonObject { ["type"] = "string", ["enum"] = values };

			kindsProperty["description"] =
				$"Optional filters by addon type: {string.Join(", ", _searchableTypes.Select(type => $"'{type}'"))}. " +
				"Omit the parameter to use all available kinds.";
		}

		/// <summary>
		/// Converts the specified addon type filters into <see cref="AddonKind"/> flags. An omitted or
		/// empty filter selects every available kind.
		/// </summary>
		/// <returns>The error message of the unknown filter values, or <see langword="null"/> when the filters are valid.</returns>
		private string? TryParseKinds(IEnumerable<string>? values, out AddonKind kinds)
		{
			kinds = _allSearchableKinds;

			if (values is null)
				return null;

			var result = AddonKind.None;
			foreach (var value in values)
			{
				if (string.IsNullOrWhiteSpace(value))
					continue;

				if (!_kindByType.TryGetValue(NormalizeType(value), out var kind))
					return $"Unknown addon kind '{value}'. Valid kinds: {string.Join(", ", _searchableTypes)}.";

				result |= kind;
			}

			if (result != AddonKind.None)
				kinds = result;

			return null;
		}

		private static string NormalizeType(string type)
		{
			return type.Replace('\\', '/').Trim().Trim('/');
		}

		private static ReactiveToolResult CreateError(string message, MaterialIconKind icon = MaterialIconKind.Search)
		{
			return new ReactiveToolResult
			{
				StatusIcon = icon,
				ResultContent = message,
				UseMarkdown = true
			}.CompleteWithError();
		}
	}
}
