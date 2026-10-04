using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Prompting;
using RCLargeLanguageModels.Metadata;

namespace LLMDesktopAssistant.LLM.Services
{
	public interface IChatSummarizationService
	{
		/// <summary>
		/// Tries to summarize the chat using the usage metadata.
		/// </summary>
		/// <param name="lastUsageMetadata">The usage metadata of the last chat message. This is used to determine if a summary should be generated.</param>
		Task TrySummarizeChatAsync(IUsageMetadata lastUsageMetadata, CancellationToken cancellationToken = default);

		/// <summary>
		/// Summarizes the message with previous messages and summary.
		/// Places summary into this message as <see cref="ContextCheckpoint"/>.
		/// </summary>
		/// <param name="message">The message to summarize. This will be updated with the summary if successful.</param>
		/// <returns>The outcome of the summarization attempt.</returns>
		Task<SummarizationOutcome> SummarizeMessageWithPreviousMessagesAsync(ChatMessage message, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// The outcome of a <see cref="IChatSummarizationService.SummarizeMessageWithPreviousMessagesAsync"/> call.
	/// </summary>
	public enum SummarizationOutcome
	{
		/// <summary>
		/// The summary was successfully generated and placed into the message.
		/// </summary>
		Success,

		/// <summary>
		/// The summarizer model is not configured or could not be found.
		/// </summary>
		ModelUnavailable,

		/// <summary>
		/// The summary generation was started but failed.
		/// </summary>
		Failed
	}
}