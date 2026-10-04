using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Addons.Search
{
	/// <summary>
	/// The provider of one addon kind for the addon tools ('addon-search', 'addon-list_available' and
	/// 'addon-info'): it selects the addons available to the calling agent, ranks them and renders them
	/// as a prompt fragment.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The contract is intentionally tool-scoped: the provider returns a ready-to-embed markdown body
	/// instead of a structured result, because the tools are the only consumers and every addon kind needs
	/// its own presentation (tools render their argument schema, skills render the loading hint, etc.).
	/// </para>
	/// <para>
	/// The group header ('<see cref="Title"/> — <see cref="UsageHint"/>') is rendered by the tools,
	/// the provider renders only the body. Implementations backed by the typed addon services can
	/// inherit <see cref="AddonAgenticSearchProvider{TAddon}"/>.
	/// </para>
	/// <para>
	/// <see cref="List"/> renders only the names of the available addons and <see cref="Info"/> renders
	/// the detailed representation of the single addon with the exact specified name.
	/// </para>
	/// </remarks>
	public interface IAddonAgenticSearchProvider
	{
		/// <summary>
		/// Gets the kind of the addons served by this provider.
		/// </summary>
		AddonKind Kind { get; }

		/// <summary>
		/// Gets the display title of the result group (for example, 'Skills').
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the usage hint rendered by the tool in the group header
		/// (for example, 'load with `skill-load` by name').
		/// </summary>
		string UsageHint { get; }

		/// <summary>
		/// Searches the addons available to the specified agent and renders the matched ones
		/// as a markdown body (without the group header).
		/// </summary>
		/// <param name="query">The free-form search query.</param>
		/// <param name="agent">The agent the addons are resolved for. Use <see langword="null"/> to disable agent-specific filters.</param>
		/// <param name="maxResults">The maximum number of matches to render.</param>
		/// <param name="detailed">Whether to render the detailed representation of every match.</param>
		/// <returns>The markdown body with the matches, or <see langword="null"/> when nothing matched.</returns>
		string? Search(string query, ChatAgentDescriptor? agent, int maxResults, bool detailed);

		/// <summary>
		/// Renders the names of every addon of this kind available to the specified agent as a markdown
		/// list body (without the group header), or <see langword="null"/> when there are no addons.
		/// </summary>
		/// <param name="agent">The agent the addons are resolved for. Use <see langword="null"/> to disable agent-specific filters.</param>
		string? List(ChatAgentDescriptor? agent);

		/// <summary>
		/// Renders the detailed representation of the addon with the exact specified name as a markdown
		/// body (without the group header), or <see langword="null"/> when no addon of this kind has such name.
		/// </summary>
		/// <param name="name">The exact name of the addon.</param>
		/// <param name="agent">The agent the addons are resolved for. Use <see langword="null"/> to disable agent-specific filters.</param>
		string? Info(string name, ChatAgentDescriptor? agent);
    }
}
