using System.Collections.Concurrent;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Consents
{
	[Service(typeof(IToolMemorizationService))]
	public class ToolMemorizationService : IToolMemorizationService
	{
		private readonly ConcurrentDictionary<(int ChatId, string ToolName), MemorizedDecision> _memorized = [];

		public void PushTaskAsyncScope()
		{
			ToolConsentTaskScope.Current = new();
		}

		public void MemorizeConsent(Chat? chat, string toolName, ToolApprovalResult approval)
		{
			// "Always" is persisted into the toolset by the caller, "Once" is not memorized at all.
			if (chat == null || approval.Memorization is ToolApprovalMemorization.Once or ToolApprovalMemorization.Always)
				return;

			if (approval.Memorization is ToolApprovalMemorization.Task && ToolConsentTaskScope.Current is { } currentScope)
			{
				currentScope.memorized[toolName] = new MemorizedDecision(approval.Approved, approval.Notes);
			}
			else
			{
				_memorized[(chat.Id, toolName)] = new MemorizedDecision(approval.Approved, approval.Notes);
			}
		}

		public IEnumerable<MemorizedConsentInfo> GetMemorizedConsents(Chat? chat)
		{
			if (chat == null)
				return [];

			return _memorized
				.Where(kv => kv.Key.ChatId == chat.Id)
				.Select(kv => new MemorizedConsentInfo(kv.Key.ToolName, kv.Value.Approved, kv.Value.Notes));
		}

		public bool ForgetConsent(Chat? chat, string toolName)
		{
			return chat != null && _memorized.TryRemove((chat.Id, toolName), out _);
		}

		public void ClearConsents(Chat? chat)
		{
			if (chat == null)
				return;

			foreach (var key in _memorized.Keys.Where(k => k.ChatId == chat.Id).ToList())
				_memorized.TryRemove(key, out _);
		}

		public bool TryGetMemorizedDecision(Chat? chat, string toolName, out ToolPolicyDecision decision, out string? message)
		{
			MemorizedDecision memorized = default;
			bool hasDecision = false;

			if (ToolConsentTaskScope.Current is { } currentScope)
				if (currentScope.memorized.TryGetValue(toolName, out memorized))
					hasDecision = true;

			if (!hasDecision && chat != null && _memorized.TryGetValue((chat.Id, toolName), out memorized))
				hasDecision = true;

			if (hasDecision)
			{
				decision = memorized.Approved ? ToolPolicyDecision.Approve : ToolPolicyDecision.Disallow;
				message = memorized.Approved ? null :
					"User has previously denied this tool." + (string.IsNullOrEmpty(memorized.Notes) ? "" : $" Reason: {memorized.Notes}.");
				return true;
			}

			decision = ToolPolicyDecision.None;
			message = null;
			return false;
		}
	}
}
