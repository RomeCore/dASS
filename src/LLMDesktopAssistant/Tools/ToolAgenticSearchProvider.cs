using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// The addon tools provider for tools of every source (native, MCP and scripted).
	/// </summary>
	/// <remarks>
	/// Tools can be hidden (just like any other addon): a hidden tool is excluded from the toolset sent
	/// to the model, but stays callable by name or alias once discovered — so the addon tools are the only
	/// way to find it. The argument schema is rendered in the detailed mode, because it is the schema
	/// actually used by the model.
	/// </remarks>
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class ToolAgenticSearchProvider(
		IAddonSetCollector<ToolInfo> collector,
		IAddonSearchService<ToolInfo> searchService)
		: AddonAgenticSearchProvider<ToolInfo>(collector, searchService)
	{
		private static readonly JsonSerializerOptions _argSchemaSerializationOptions = new()
		{
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = true,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};

		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.Tool;

		/// <inheritdoc/>
		public override string Title => "Tools";

		/// <inheritdoc/>
		public override string UsageHint => "call it DIRECTLY by name (same as you call normal tools, " +
			"regardless of your tool list in the system prompt) or pass to `agent-call` via `allowedTools`";

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, ToolInfo tool, bool detailed)
		{
			var aliases = tool.Aliases.Count > 0 ? $"(aliases: {string.Join(", ", tool.Aliases)})" : null;
			AddonSearchFormatting.AppendItem(builder, tool.Name, tool.Description, aliases);

			var parameters = tool.ArgumentSchema.ToJsonString(_argSchemaSerializationOptions);
			if (parameters.Length > 0)
				builder.Append("  - arguments: ").AppendLine(parameters);

			if (!detailed)
				return;

			builder.Append("  - source: ").AppendLine(tool.ToolSource.ToString().ToLowerInvariant());

			AddonSearchFormatting.AppendMetadata(builder, tool.Tags, tool.SourcePack?.Name, tool.Path);
		}
	}
}
