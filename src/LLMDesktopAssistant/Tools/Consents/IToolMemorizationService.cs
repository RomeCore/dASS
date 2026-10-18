using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Consents
{
	public interface IToolMemorizationService
	{
		void PushTaskAsyncScope();

		void MemorizeConsent(Chat? chat, string toolName, ToolApprovalResult approval);

		bool TryGetMemorizedDecision(Chat? chat, string toolName, out ToolPolicyDecision decision, out string? message);

		/// <summary>
		/// Gets the session-memorized consent decisions for the given chat.
		/// </summary>
		/// <param name="chat">The chat to list the decisions for, or <see langword="null"/>.</param>
		/// <returns>The memorized consent decisions of the chat.</returns>
		IEnumerable<MemorizedConsentInfo> GetMemorizedConsents(Chat? chat);

		/// <summary>
		/// Forgets the session-memorized consent decision for the given tool in the given chat.
		/// </summary>
		/// <param name="chat">The chat the decision belongs to, or <see langword="null"/>.</param>
		/// <param name="toolName">The name of the tool.</param>
		/// <returns><see langword="true"/> if a memorized decision was removed; otherwise, <see langword="false"/>.</returns>
		bool ForgetConsent(Chat? chat, string toolName);

		/// <summary>
		/// Forgets all session-memorized consent decisions of the given chat.
		/// </summary>
		/// <param name="chat">The chat to clear the decisions for, or <see langword="null"/>.</param>
		void ClearConsents(Chat? chat);
	}
}