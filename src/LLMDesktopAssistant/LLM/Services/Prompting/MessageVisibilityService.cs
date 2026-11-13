using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Agents;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <inheritdoc cref="IMessageVisibilityService"/>
	[ChatService(typeof(IMessageVisibilityService))]
	public class MessageVisibilityService(
		IChatSettingsService chatSettings,
		IAgentManagementService agentManager) : IMessageVisibilityService
	{
		/// <inheritdoc/>
		public MessageVisibilityResult CheckVisibility(BranchedMessage message, ChatAgentDescriptor agent)
		{
			// A message disabled for agents is invisible to every agent - including its own sender. The check is
			// unconditional and first so that no branch (not even the assistant same-agent shortcut) can bypass it.
			if (message?.Message?.IsDisabledForAgents == true)
				return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

			if (message?.Message is UserMessage userMessage)
			{
				var readFilter = agent.Read.GetEffectiveReadFilters(chatSettings.Settings).User;

				// 4th case:
				// If its a white list, then 'contains' must return true to skip this check -> true == true
				// If its a black list, then 'contains' must return false to skip this check -> false == false
				if ((!readFilter.Visible) ||
					(userMessage.Visibility is MessageVisibility.OnlyUsers && !agent.Info.IdentifyAsUser) ||
					(userMessage.Visibility is MessageVisibility.OnlyAgents && agent.Info.IdentifyAsUser) ||
					(userMessage.Visibility is MessageVisibility.RevealAfterSend && !userMessage.IsRevealed && agent.Info.IdentifyAsUser) ||
					(userMessage.VisibleTo.Contains(agent.Id.ToString()) != userMessage.IsVisibleToWhiteList))
					return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

				var identity = readFilter.Identity is MessageAuthorIdentity.Default
					? MessageAuthorIdentity.NamedUser
					: readFilter.Identity;

				return new MessageVisibilityResult(true, false, readFilter.VisibleParts, identity);
			}
			else if (message?.Message is AssistantMessage assistantMessage)
			{
				var messageAgentId = assistantMessage.SenderAgentId;
				if (messageAgentId == agent.Id)
					return new MessageVisibilityResult(true, true, MessagePartsFacet.All, MessageAuthorIdentity.Default);

				var readFilters = agent.Read.GetEffectiveReadFilters(chatSettings.Settings);
				var readFilter = assistantMessage.IsUserLike ? readFilters.User : readFilters.Agent;

				var senderAgent = agentManager.TryGetAgentDescriptor(assistantMessage.SenderAgentId);

				if (senderAgent is not null)
				{
					var defaultShares = senderAgent.Read.GetEffectiveDefaultShareFilters(chatSettings.Settings);
					var defaultSenderShare = agent.Info.IdentifyAsUser ? defaultShares.User : defaultShares.Agent;
					var senderShare = senderAgent.Read.ParticipantsShareFilters.GetValueOrDefault(agent.Id);

					bool shareVisible = senderShare is { OverrideVisible: true } ? senderShare.Visible : defaultSenderShare.Visible;
					if (!shareVisible || !readFilter.Visible)
						return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

					var compoundVisibility = defaultSenderShare.VisibleMessages;
					if (senderShare != null)
						compoundVisibility = (compoundVisibility & ~senderShare.OverridenVisibleMessages)
							| (senderShare.VisibleMessages & senderShare.OverridenVisibleMessages);
					compoundVisibility &= readFilter.VisibleMessages;

					bool visibilityResult =
						(compoundVisibility is MessageVisibilityFacet.Unknown) ||
						(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithToolCalls) && assistantMessage.ToolCalls.Count > 0) ||
						(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithoutToolCalls) && assistantMessage.ToolCalls.Count == 0);
					if (!visibilityResult)
						return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

					var compoundParts = defaultSenderShare.VisibleParts;
					if (senderShare != null)
						compoundParts = (compoundParts & ~senderShare.OverridenVisibleParts)
							| (senderShare.VisibleParts & senderShare.OverridenVisibleParts);
					compoundParts &= readFilter.VisibleParts;

					var compoundIdentity = defaultSenderShare.Identity;
					if (senderShare != null && senderShare.Identity is not MessageAuthorIdentity.Default)
						compoundIdentity = senderShare.Identity;
					if (readFilter.Identity is not MessageAuthorIdentity.Default)
						compoundIdentity = readFilter.Identity;
					if (compoundIdentity is MessageAuthorIdentity.Default)
						compoundIdentity = assistantMessage.IsUserLike
							? MessageAuthorIdentity.NamedUser
							: MessageAuthorIdentity.NamedAgent;

					return new MessageVisibilityResult(true, false, compoundParts, compoundIdentity);
				}
				else
				{
					// If agent is not found (unknown, or removed), we don't apply the share filters - every agent can read every parts of the message.
					if (!readFilter.Visible)
						return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

					var compoundVisibility = readFilter.VisibleMessages;

					bool visibilityResult =
						(compoundVisibility is MessageVisibilityFacet.Unknown) ||
						(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithToolCalls) && assistantMessage.ToolCalls.Count > 0) ||
						(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithoutToolCalls) && assistantMessage.ToolCalls.Count == 0);
					if (!visibilityResult)
						return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

					var compoundParts = readFilter.VisibleParts;

					var compoundIdentity = readFilter.Identity;
					if (compoundIdentity is MessageAuthorIdentity.Default)
						compoundIdentity = assistantMessage.IsUserLike
							? MessageAuthorIdentity.NamedUser
							: MessageAuthorIdentity.NamedAgent;

					return new MessageVisibilityResult(true, false, compoundParts, compoundIdentity);
				}
			}
			throw new InvalidOperationException("Invalid message type: " + message?.Message?.GetType() ?? "null");
		}
	}
}
