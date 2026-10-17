using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	/// <summary>
	/// The chat-scoped tool execution entry point. Until P4 replaces it with the orchestrator plus the
	/// dispatcher, it sequences the Application-scoped stage services for one chat tool call.
	/// </summary>
	public interface IChatToolExecutionService
	{
		/// <summary>
		/// Executes a tool call asynchronously.
		/// </summary>
		/// <param name="partialFunctionToolCall">The model's streaming call, or <see langword="null"/>.</param>
		/// <param name="message">The message that owns the call.</param>
		/// <param name="toolCall">The call to process.</param>
		/// <param name="toolInfo">The tool, or <see langword="null"/> when it does not exist.</param>
		/// <param name="cancellationToken">Cancellation of the run.</param>
		Task ExecuteAsync(PartialFunctionToolCall? partialFunctionToolCall,
			AssistantMessage message, ToolCall toolCall, ToolInfo? toolInfo, CancellationToken cancellationToken = default);
	}
}
