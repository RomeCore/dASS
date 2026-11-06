using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Hooks
{
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class HookAgenticSearchProvider(
		IAddonSetCollector<HookInfo> collector,
		IAddonSearchService<HookInfo> searchService)
		: AddonAgenticSearchProvider<HookInfo>(collector, searchService)
	{
		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.Hook;

		/// <inheritdoc/>
		public override string Title => "Hooks";

		/// <inheritdoc/>
		public override string UsageHint => "edit them using source path";

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, HookInfo hook, bool detailed)
		{
			AddonSearchFormatting.AppendItem(builder, hook.Name, hook.Description);

			builder.Append("  - execution flow: ").AppendLine(KebabEnumNames<HookExecutionFlow>.Serialize(hook.ExecutionFlow));

			if (!detailed)
				return;

			AddonSearchFormatting.AppendMetadata(builder, hook.Tags, hook.SourcePack?.Name, hook.Path);
		}
	}
}
