using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Consents;

/// <summary>
/// Lets a tool persist user consent decisions ("remember") for self-handled confirmations.
/// Created by the tool execution pipeline and exposed to tools via
/// <see cref="LLMDesktopAssistant.Tools.Execution.Domain.IToolExecutionContext.Consent"/>.
/// </summary>
public sealed class ToolConsentMemorizationContext
{
	private readonly IToolMemorizationService _memorizationService;
	private readonly Chat? _chat;
	private readonly string _toolName;
	private readonly Action<bool>? _memorizeAlways;

	/// <summary>
	/// Initializes a new instance of the <see cref="ToolConsentMemorizationContext"/> class.
	/// </summary>
	/// <param name="approvalService">The approval service used to store in-memory decisions.</param>
	/// <param name="chat">The chat where the tool is executed, or <see langword="null"/>.</param>
	/// <param name="toolName">The name of the tool.</param>
	/// <param name="memorizeAlways">
	/// The callback that persists an "always" decision into the agent toolset, or <see langword="null"/>.
	/// </param>
	public ToolConsentMemorizationContext(IToolMemorizationService memorizationService, Chat? chat, string toolName,
		Action<bool>? memorizeAlways = null)
	{
		_memorizationService = memorizationService;
		_chat = chat;
		_toolName = toolName;
		_memorizeAlways = memorizeAlways;
	}

	/// <summary>
	/// Memorizes the consent decision according to the selected <see cref="ToolApprovalResult.Memorization"/>.
	/// <see cref="ToolApprovalMemorization.Always"/> is persisted into the agent toolset via the
	/// "always" callback; other memorized options are stored in-memory by the approval service.
	/// </summary>
	/// <param name="approval">The approval produced by the confirmation UI.</param>
	public void Memorize(ToolApprovalResult approval)
	{
		if (approval.Memorization == ToolApprovalMemorization.Always)
		{
			_memorizeAlways?.Invoke(approval.Approved);
			return;
		}

		_memorizationService.MemorizeConsent(_chat, _toolName, approval);
	}
}
