using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Serves the chat's prompt contexts - the SCM sections, both native and scriptable - to the addon tools
	/// ('addon-search', 'addon-list_available' and 'addon-info'), so an agent can tell the user which sections
	/// shape its prompt and where their text lands.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A prompt context is prompt content, not an action: the agent cannot inject, refresh, reorder or tear one
	/// down, and it never runs the script. The catalogue is therefore read-only advice - every card names the
	/// section exactly as it appears in the prompt, so the model can match a tag it sees in its own context
	/// (for example <c>&lt;git-status&gt;</c>) to the addon that produced it.
	/// </para>
	/// <para>
	/// The candidates come from <see cref="IAddonSetCollector{T}.GetAddonsForAgent"/> - the agent's effective
	/// context set, with the enablement settings and per-agent overrides already applied. Prompt contexts are
	/// per-agent, so a section disabled for the calling agent is not searchable.
	/// </para>
	/// </remarks>
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class PromptContextAgenticSearchProvider(
		IAddonSetCollector<PromptContextInfo> collector,
		IAddonSearchService<PromptContextInfo> searchService)
		: AddonAgenticSearchProvider<PromptContextInfo>(collector, searchService)
	{
		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.PromptContext;

		/// <inheritdoc/>
		public override string Title => "Prompt contexts";

		/// <inheritdoc/>
		public override string UsageHint =>
			"injected into the prompt automatically; you cannot run, load or refresh one - " +
			"describe them to the user, never try to invoke them";

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, PromptContextInfo context, bool detailed)
		{
			AddonSearchFormatting.AppendItem(builder, context.Name, context.Description, BuildSuffix(context));

			builder.Append("  - kind: ").AppendLine(context.Kind.ToString().ToLowerInvariant());

			if (!detailed)
				return;

			builder.Append("  - variability: ").AppendLine(context.Variability.ToString().ToLowerInvariant());

			if (IsScript(context))
				builder.Append("  - timeout: ").Append(context.TimeoutMs).AppendLine(" ms");

			AddonSearchFormatting.AppendMetadata(builder, context.Tags, context.SourcePack?.Name, context.Path);
		}

		/// <summary>
		/// Renders the trailing parenthesised suffix of the card: where the text of the context lands in the
		/// prompt, which is the one thing an agent cannot learn from the name alone.
		/// </summary>
		private static string? BuildSuffix(PromptContextInfo context)
		{
			var landing = context.Kind switch
			{
				PromptContextProviderKind.Anchored => "built-in section, frozen into the system prompt header",
				PromptContextProviderKind.LiveTail => "script, live-tail - lands in the current request only",
				PromptContextProviderKind.Supersede => "script, supersede - persisted, re-injected when the old stamp goes stale",
				_ => null
			};

			return landing is null ? null : "(" + landing + ")";
		}

		/// <summary>
		/// Determines whether the context is produced by a script (as opposed to native code). Only a script
		/// carries a timeout, because only a script can take time to run.
		/// </summary>
		private static bool IsScript(PromptContextInfo context)
		{
			return context.Kind is PromptContextProviderKind.LiveTail or PromptContextProviderKind.Supersede;
		}
	}
}
