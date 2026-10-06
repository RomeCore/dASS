using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The addon tools provider for Lua scripts: renders the script name, description and, in the
	/// detailed mode, the namespace, the manuals and the common addon metadata.
	/// </summary>
	/// <remarks>
	/// The effective script set is chat-level (see <see cref="LuaScriptsetCollector.GetAddonsForChat"/>),
	/// and the native API hosts are excluded because they are not user addons.
	/// </remarks>
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class LuaScriptAgenticSearchProvider(
		IAddonSetCollector<LuaScriptInfo> collector,
		IAddonSearchService<LuaScriptInfo> searchService)
		: AddonAgenticSearchProvider<LuaScriptInfo>(collector, searchService)
	{
		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.LuaScript;

		/// <inheritdoc/>
		public override string Title => "Lua scripts";

		/// <inheritdoc/>
		public override string UsageHint => "available in Lua code via their namespace";

		/// <inheritdoc/>
		protected override IEnumerable<LuaScriptInfo> GetCandidates(ChatAgentDescriptor? agent)
		{
			return Collector.GetAddonsForChat();
		}

		/// <inheritdoc/>
		protected override bool Include(LuaScriptInfo addon)
		{
			return !addon.IsNative;
		}

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, LuaScriptInfo script, bool detailed)
		{
			AddonSearchFormatting.AppendItem(builder, script.Name, script.Description);

			if (!detailed)
				return;

			if (!string.IsNullOrEmpty(script.Namespace))
				builder.Append("  - namespace: ").AppendLine(script.Namespace);

			var manuals = AddonSearchFormatting.Inline(script.Manuals);
			if (manuals.Length > 0)
				builder.Append("  - manuals: ").AppendLine(manuals);

			AddonSearchFormatting.AppendMetadata(builder, script.Tags, script.SourcePack?.Name, script.HomeDirectory);
		}
	}
}
