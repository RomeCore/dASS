using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Owns a chat turn: the <see cref="ChatExecutionLevel.Operation"/> execution scope, the choice between inserting
	/// a message and queueing it, the drain of the follow-up queue, and the single generation that covers the batch.
	/// <para/>
	/// A turn and an <c>Operation</c> token share one lifetime, so whoever takes the operation owns the turn. Two turns
	/// never interleave: the second operation cancels the first ("newest wins"), and a cancelled turn does not advance
	/// the queue.
	/// </summary>
	public interface IChatOperationService
	{
		/// <summary>
		/// Continues the current conversation: generates a response and then drains whatever arrived while it ran.
		/// </summary>
		Task ContinueGenerationAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Sends a message as a turn. With pending follow-ups the message joins the tail of the queue, the queue is
		/// inserted first and one answer covers the batch; with an empty queue the message is inserted at once.
		/// </summary>
		Task SendUserInputAsync(UserInputState userInput, bool generate, CancellationToken cancellationToken = default);

		/// <summary>
		/// Commits an edit: stops the running turn, waits for it to unwind, replaces the message, then drains the queue
		/// after the new branch.
		/// </summary>
		Task SendEditedUserInputAsync(int messageIndex, UserInputState userInput, bool generate,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Puts a message into the follow-up queue without running anything. Takes <em>no</em> execution scope on
		/// purpose: the queue must outlive the run it is filled during. An entry with
		/// <see cref="FollowUpInjectionLevel.Immediate"/> then stops the run and drains.
		/// </summary>
		Task QueueUserInputAsync(UserInputState userInput, bool generate, CancellationToken cancellationToken = default);

		/// <summary>
		/// Drains the pending follow-ups as a turn: inserts them in order, runs their commands, and generates once if
		/// any entry asked for an answer.
		/// </summary>
		Task FlushFollowUpsAsync(bool generate, CancellationToken cancellationToken = default);

		/// <summary>
		/// Hard stop: cancels the running turn and leaves the queue untouched.
		/// </summary>
		Task StopGenerationAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Stops the running turn, waits until the history is quiet, then drains the queue and generates again.
		/// </summary>
		Task StopAndFlushAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Regenerates the message at <paramref name="messageIndex"/>: the answer comes first, the queue after it.
		/// </summary>
		Task RegenerateMessageAsync(int messageIndex, CancellationToken cancellationToken = default);

		/// <summary>
		/// Resends from the message at <paramref name="messageIndex"/>: the answer comes first, the queue after it.
		/// </summary>
		Task ResendMessageAsync(int messageIndex, CancellationToken cancellationToken = default);

		/// <summary>
		/// Switches the visible branch of the message at <paramref name="messageIndex"/>.
		/// </summary>
		void SwitchBranch(int messageIndex, int branchIndex);

		/// <summary>
		/// Replaces the message at <paramref name="messageIndex"/> without generating.
		/// </summary>
		void EditMessage(int messageIndex, ChatMessage newMessage);

		/// <summary>
		/// Deletes the message at <paramref name="messageIndex"/> together with its descendants.
		/// </summary>
		void DeleteMessageWithDescendants(int messageIndex);
	}
}
