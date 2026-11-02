using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Interface for chat execution service that calls LLM services, executes tools, plans and puts result messages into a <see cref="Chat"/>.
	/// </summary>
	public interface IChatExecutionService
	{
		/// <summary>
		/// Generates a response asynchronously and puts it into a <see cref="IChatStorageService"/>.
		/// </summary>
		/// <param name="cancellationToken">Token for cancellation of the operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task GenerateResponseAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Generates a response asynchronously using specific agent and puts it into a <see cref="IChatStorageService"/>.
		/// </summary>
		/// <param name="agentId">The identifier of the agent to use.</param>
		/// <param name="agentStageId">The identifier of the stage within the agent to use.</param>
		/// <param name="cancellationToken">Token for cancellation of the operation.</param>
		/// <returns><see langword="true"/> when the call stopped at a follow-up barrier instead of finishing the agent's
		/// cycle; the caller must then end the sequence and let the turn drain the queue.</returns>
		Task<bool> GenerateResponseWithAgentAsync(Guid agentId, Guid agentStageId, CancellationToken cancellationToken = default);
	}
}