using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// Creates contexts for chat tools. It captures the home chat, so every invocation sees the chat
	/// scope and the chat's consent memorization.
	/// </summary>
	/// <remarks>
	/// The factory is stamped on the per-chat tool descriptor clone before the descriptor is frozen.
	/// The "always" consent callback is built per invocation from the message's sender agent, because
	/// the persisted decision belongs to that agent's toolset.
	/// </remarks>
	public sealed class ChatToolContextFactory(
		Chat chat,
		IToolMemorizationService memorization,
		IAgentManagementService agentManager,
		IChatSettingsService chatSettings) : IToolExecutionContextFactory
	{
		/// <summary>
		/// Builds the chat context for one invocation.
		/// </summary>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		/// <returns>The chat context with the chat scope and consent memorization attached.</returns>
		/// <exception cref="InvalidOperationException">
		/// The host is not a chat message, so the invocation cannot belong to a chat.
		/// </exception>
		public IToolExecutionContext CreateContext(in ToolContextCreationParams parameters)
		{
			if (parameters.Host is not ChatMessage message)
				throw new InvalidOperationException("A chat tool invocation requires a chat message host.");

			var call = parameters.Call;

			Action<bool>? persistAlways = null;
			if (message is AssistantMessage { SenderAgentId: var senderAgentId } && senderAgentId != Guid.Empty)
			{
				var sender = agentManager.GetAgentDescriptor(senderAgentId);
				persistAlways = approved =>
					ToolConsentPersister.MemorizeAlways(sender, chatSettings, call.ToolName, approved);
			}

			var consent = new ToolConsentMemorizationContext(memorization, chat, call.ToolName, persistAlways);
			return new ChatToolExecutionContext(chat, message, parameters, consent);
		}
	}
}
