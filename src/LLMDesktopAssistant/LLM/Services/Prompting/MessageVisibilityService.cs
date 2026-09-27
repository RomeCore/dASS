using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Settings;

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

				var senderAgent = agentManager.GetAgentDescriptor(assistantMessage.SenderAgentId);

				var readFilters = agent.Read.GetEffectiveReadFilters(chatSettings.Settings);
				var readFilter = assistantMessage.IsUserLike ? readFilters.User : readFilters.Agent;
				
				var defaultShares = senderAgent.Read.GetEffectiveDefaultShareFilters(chatSettings.Settings);
				var defaultShare = agent.Info.IdentifyAsUser ? defaultShares.User : defaultShares.Agent;
				var share = senderAgent.Read.ParticipantsShareFilters.GetValueOrDefault(agent.Id);

				var compoundVisibility = defaultShare.VisibleMessages;
				if (share != null)
					compoundVisibility = (compoundVisibility & ~share.OverridenVisibleMessages)
						| (share.VisibleMessages & share.OverridenVisibleMessages);
				compoundVisibility &= readFilter.VisibleMessages;

				bool shareVisible = share is { OverrideVisible: true } ? share.Visible : defaultShare.Visible;

				bool visibilityResult =
					shareVisible && readFilter.Visible &&
					((compoundVisibility is MessageVisibilityFacet.Unknown) ||
					(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithToolCalls) && assistantMessage.ToolCalls.Count > 0) ||
					(compoundVisibility.HasFlag(MessageVisibilityFacet.MessagesWithoutToolCalls) && assistantMessage.ToolCalls.Count == 0));

				if (!visibilityResult)
					return new MessageVisibilityResult(false, false, MessagePartsFacet.None, MessageAuthorIdentity.Default);

				var compoundParts = defaultShare.VisibleParts;
				if (share != null)
					compoundParts = (compoundParts & ~share.OverridenVisibleParts)
						| (share.VisibleParts & share.OverridenVisibleParts);
				compoundParts &= readFilter.VisibleParts;

				var compoundIdentity = defaultShare.Identity;
				if (share != null && share.Identity is not MessageAuthorIdentity.Default)
					compoundIdentity = share.Identity;
				if (readFilter.Identity is not MessageAuthorIdentity.Default)
					compoundIdentity = readFilter.Identity;
				if (compoundIdentity is MessageAuthorIdentity.Default)
					compoundIdentity = assistantMessage.IsUserLike
						? MessageAuthorIdentity.NamedUser
						: MessageAuthorIdentity.NamedAgent;

				return new MessageVisibilityResult(true, false, compoundParts, compoundIdentity);
			}
			throw new InvalidOperationException("Invalid message type: " + message?.Message?.GetType() ?? "null");
		}
	}
}
