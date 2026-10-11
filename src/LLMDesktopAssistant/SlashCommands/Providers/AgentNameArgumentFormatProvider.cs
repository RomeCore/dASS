using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The format provider of the <c>/call</c> command's <c>agent</c> argument: validates that the value names an
	/// existing chat agent, converts it to that agent's id and completes it against the chat's agents.
	/// </summary>
	/// <remarks>
	/// It resolves over the union of chat-local and global agents. Chat-local agents come first (their order is
	/// preserved by <see cref="IAgentManagementService.ListAgents"/>), so a local agent shadows a global one with the
	/// same name. Validation runs at bind time, which blocks the send.
	/// </remarks>
	public sealed class AgentNameArgumentFormatProvider(IAgentManagementService agentManager)
		: ISlashCommandArgumentFormatProvider
	{
		private static readonly StringComparison NameComparison = StringComparison.OrdinalIgnoreCase;

		/// <inheritdoc/>
		public bool CanComplete => true;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			if (Find(raw) is not null)
			{
				error = null;
				return true;
			}

			error = Locale.GetFormattedKey("command.error.agent_not_found", raw);
			return false;
		}

		/// <inheritdoc/>
		public object? Convert(string raw) => Find(raw)?.Id;

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
		{
			foreach (var (agent, isGlobal) in agentManager.ListAgents())
			{
				var name = agent.Info.Name;
				if (!name.StartsWith(prefix, NameComparison))
					continue;

				yield return new SlashCommandCompletionItem
				{
					Value = name,
					Display = isGlobal ? $"{name} · global" : name,
					Description = string.IsNullOrEmpty(agent.Info.Description)
						? null
						: Locale.GetConstKey(agent.Info.Description)
				};
			}
		}

		/// <summary>
		/// Resolves a name to a chat agent, chat-local first, or <see langword="null"/> when no agent carries it.
		/// </summary>
		private ChatAgentDescriptor? Find(string raw)
		{
			return agentManager.ListAgents()
				.Select(entry => entry.Agent)
				.FirstOrDefault(agent => string.Equals(agent.Info.Name, raw, NameComparison));
		}
	}
}
