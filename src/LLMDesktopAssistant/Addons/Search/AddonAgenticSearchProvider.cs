using System.Text;
using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Addons.Search
{
	/// <summary>
	/// The base class for <see cref="IAddonAgenticSearchProvider"/> implementations backed by the typed
	/// addon services: <see cref="IAddonSetCollector{T}"/> for the candidates and
	/// <see cref="IAddonSearchService{T}"/> for the BM25 ranking.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addon served by the provider.</typeparam>
	public abstract class AddonAgenticSearchProvider<TAddon> : IAddonAgenticSearchProvider
		where TAddon : AddonBase<TAddon>
	{
		private readonly IAddonSetCollector<TAddon> _collector;
		private readonly IAddonSearchService<TAddon> _searchService;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonAgenticSearchProvider{TAddon}"/> class.
		/// </summary>
		protected AddonAgenticSearchProvider(IAddonSetCollector<TAddon> collector,
			IAddonSearchService<TAddon> searchService)
		{
			_collector = collector;
			_searchService = searchService;
		}

		/// <summary>
		/// Gets the collector the provider resolves the candidate addons with.
		/// </summary>
		protected IAddonSetCollector<TAddon> Collector => _collector;

		/// <inheritdoc/>
		public abstract AddonKind Kind { get; }

		/// <inheritdoc/>
		public abstract string Title { get; }

		/// <inheritdoc/>
		public abstract string UsageHint { get; }

		/// <summary>
		/// Gets the candidate addons for the specified agent. Defaults to the effective addon set of the
		/// agent (including hidden addons — the same set the agent can actually load and use).
		/// </summary>
		protected virtual IEnumerable<TAddon> GetCandidates(ChatAgentDescriptor? agent)
		{
			return agent is not null ? _collector.GetAddonsForAgent(agent) : _collector.GetAvailableAddons();
		}

		/// <summary>
		/// Determines whether the specified candidate should be searchable.
		/// </summary>
		protected virtual bool Include(TAddon addon)
		{
			return true;
		}

		/// <summary>
		/// Renders the specified addon as markdown lines (without the group header).
		/// </summary>
		protected abstract void AppendAddon(StringBuilder builder, TAddon addon, bool detailed);

		/// <inheritdoc/>
		public string? List(ChatAgentDescriptor? agent)
		{
			var builder = new StringBuilder();
			foreach (var addon in GetCandidates(agent).Where(Include))
				builder.Append("- `").Append(addon.Name).AppendLine("`");

			var body = builder.ToString().TrimEnd();
			return body.Length == 0 ? null : body;
		}

		/// <inheritdoc/>
		public string? Info(string name, ChatAgentDescriptor? agent)
		{
			if (string.IsNullOrWhiteSpace(name))
				return null;

			var addon = GetCandidates(agent)
				.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

			if (addon is null)
				return null;

			var builder = new StringBuilder();
			AppendAddon(builder, addon, detailed: true);

			return builder.ToString().TrimEnd();
		}

		/// <inheritdoc/>
		public string? Search(string query, ChatAgentDescriptor? agent, int maxResults, bool detailed)
		{
			var matches = _searchService.Search(query, GetCandidates(agent).Where(Include), maxResults);
			if (matches.Count == 0)
				return null;

			var builder = new StringBuilder();
			foreach (var match in matches)
				AppendAddon(builder, match.Addon, detailed);

			return builder.ToString().TrimEnd();
		}
	}
}
