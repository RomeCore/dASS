using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatOperationService"/>
	[ChatService(typeof(IChatOperationService))]
	public class ChatOperationService(
		Chat chat,
		IChatStorageService storage,
		IChatExecutionService executor,
		IChatExecutionTokenService tokens,
		IChatMessageInsertionService insertion,
		IFollowUpChatMessageQueue followUps,
		IChatExecutionStatusService executionStatus
	) : IChatOperationService
	{
		/// <inheritdoc/>
		public async Task ContinueGenerationAsync(CancellationToken cancellationToken = default)
		{
			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);
			await RunTurnAsync(flushFirst: false, generate: true, token);
		}

		/// <inheritdoc/>
		public async Task SendUserInputAsync(UserInputState userInput, bool generate, CancellationToken cancellationToken = default)
		{
			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			if (followUps.HasAny)
			{
				// A pending queue means the user already had things to say: the new message joins the tail and the whole
				// batch is drained in order, with one answer at the end.
				followUps.Enqueue(userInput, followUps.DefaultLevel, generate, userInput.CanExecuteCommand);
				await RunTurnAsync(flushFirst: true, generate: false, token);
				return;
			}

			var inserted = await insertion.AppendUserInputAsync(userInput, editIndex: null, token);
			var result = await insertion.RunInsertedCommandAsync(inserted, generate, token);
			await RunTurnAsync(flushFirst: true, generate: result.Generate, token);
		}

		/// <inheritdoc/>
		public async Task SendEditedUserInputAsync(int messageIndex, UserInputState userInput, bool generate,
			CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			// The edit truncates the tail, so it must not start until the running turn has fully unwound.
			if (!await StopAndWaitForIdleAsync(cancellationToken))
				return;

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			var inserted = await insertion.AppendUserInputAsync(userInput, messageIndex, token);
			var result = await insertion.RunInsertedCommandAsync(inserted, generate, token);
			await RunTurnAsync(flushFirst: true, generate: result.Generate, token);
		}

		/// <inheritdoc/>
		public async Task QueueUserInputAsync(UserInputState userInput, bool generate, CancellationToken cancellationToken = default)
		{
			// No execution scope on purpose: taking one would cancel the very run this message is meant to outlive.
			var entry = followUps.Enqueue(userInput, followUps.DefaultLevel, generate, userInput.CanExecuteCommand);

			if (entry.Level == FollowUpInjectionLevel.Immediate)
				await StopAndFlushAsync(cancellationToken);
		}

		/// <inheritdoc/>
		public async Task FlushFollowUpsAsync(bool generate, CancellationToken cancellationToken = default)
		{
			if (!followUps.HasAny)
				return;

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);
			await RunTurnAsync(flushFirst: true, generate: generate, token);
		}

		/// <inheritdoc/>
		public Task StopGenerationAsync(CancellationToken cancellationToken = default)
		{
			tokens.ExecutionCancellationToken?.Cancel();
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public async Task StopAndFlushAsync(CancellationToken cancellationToken = default)
		{
			if (!await StopAndWaitForIdleAsync(cancellationToken))
				return;

			if (!followUps.HasAny)
				return;

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);
			await RunTurnAsync(flushFirst: true, generate: true, token);
		}

		/// <inheritdoc/>
		public async Task RegenerateMessageAsync(int messageIndex, CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			if (!await StopAndWaitForIdleAsync(cancellationToken))
				return;

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			var targetMessage = chat.Messages[messageIndex].Message;

			if (targetMessage is UserMessage)
			{
				throw new InvalidOperationException("Cannot regenerate a user message.");
			}
			else if (targetMessage is AssistantMessage)
			{
				storage.PlaceNewBranch(messageIndex);
				await RunTurnAsync(flushFirst: false, generate: true, token);
			}
			else
			{
				throw new InvalidOperationException("Invalid message type.");
			}
		}

		/// <inheritdoc/>
		public async Task ResendMessageAsync(int messageIndex, CancellationToken cancellationToken = default)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			if (!await StopAndWaitForIdleAsync(cancellationToken))
				return;

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, cancellationToken, out var token);

			var nextMessageIndex = messageIndex + 1;
			if (nextMessageIndex < chat.Messages.Count)
				storage.PlaceNewBranch(nextMessageIndex);

			await RunTurnAsync(flushFirst: false, generate: true, token);
		}

		/// <inheritdoc/>
		public void SwitchBranch(int messageIndex, int branchIndex)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));
			if (branchIndex < 0 || branchIndex >= chat.Messages[messageIndex].AvailableBranchesCount)
				throw new ArgumentOutOfRangeException(nameof(branchIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.SwitchBranch(messageIndex, branchIndex);
		}

		/// <inheritdoc/>
		public void EditMessage(int messageIndex, ChatMessage newMessage)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.EditMessage(messageIndex, newMessage);
		}

		/// <inheritdoc/>
		public void DeleteMessageWithDescendants(int messageIndex)
		{
			if (messageIndex < 0 || messageIndex >= chat.Messages.Count)
				throw new ArgumentOutOfRangeException(nameof(messageIndex));

			using var scope = tokens.WithToken(ChatExecutionLevel.Operation, default, out _);
			storage.DeleteMessageWithDescendants(messageIndex);
		}

		/// <summary>
		/// Runs one turn. The loop exists because a generation can be cut short by a barrier: whatever the user queued
		/// while it ran is drained here, and the answer is asked for again.
		/// </summary>
		/// <param name="flushFirst">Whether the queue is drained before the answer (a send, an edit commit, a
		/// stop-and-flush) or after it (regenerate, resend, continue).</param>
		/// <param name="generate">Whether the turn starts with an answer of its own.</param>
		/// <param name="token">The operation's execution token.</param>
		private async Task RunTurnAsync(bool flushFirst, bool generate, CancellationToken token)
		{
			var intent = generate;

			while (true)
			{
				// A cancelled turn must never advance the queue: the newer turn owns it now.
				token.ThrowIfCancellationRequested();

				if (followUps.HasAny && (flushFirst || !intent))
				{
					var outcome = await FlushAsync(token);
					intent |= outcome.Intent;

					// Nothing could be taken (another turn owns the head), so asking for an answer again would spin.
					if (!outcome.Inserted)
						return;
				}

				if (!intent)
					return;

				await executor.GenerateResponseAsync(token);

				if (!followUps.HasAny)
					return;

				flushFirst = true;
			}
		}

		/// <summary>
		/// Inserts every pending follow-up, one at a time, in order. An entry leaves the queue once its message is in
		/// the history and before its command runs, so the queue never shows an already-inserted message.
		/// </summary>
		/// <param name="token">The operation's execution token.</param>
		private async Task<FlushOutcome> FlushAsync(CancellationToken token)
		{
			var inserted = false;
			var intent = false;

			while (followUps.TryBeginNext(out var entry))
			{
				if (token.IsCancellationRequested)
				{
					followUps.Abandon(entry);
					token.ThrowIfCancellationRequested();
				}

				UserInputInsertion appended;
				try
				{
					appended = await insertion.AppendUserInputAsync(entry.UserInput, editIndex: null, token);
				}
				catch
				{
					// Nothing reached the history: the entry stays pending.
					followUps.Abandon(entry);
					throw;
				}

				followUps.Complete(entry);
				inserted = true;
				intent |= entry.GenerateIntent;

				var result = await insertion.RunInsertedCommandAsync(appended, entry.GenerateIntent, token);
				intent |= result.Generate;
			}

			return new FlushOutcome(inserted, intent);
		}

		/// <summary>
		/// Cancels the running turn and waits until it has fully unwound. The wait is observed on the execution status,
		/// not on the token service: cancelling a level erases its entry, so the token service cannot report the end of
		/// the run it just killed.
		/// </summary>
		/// <returns><see langword="false"/> when the wait was cancelled, so the caller can stop without starting a
		/// turn.</returns>
		private async Task<bool> StopAndWaitForIdleAsync(CancellationToken ct)
		{
			tokens.ExecutionCancellationToken?.Cancel();

			try
			{
				await executionStatus.WaitUntilIdleAsync(ct);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
		}

		/// <summary>
		/// The result of one drain: whether any entry was inserted, and the generation intent the batch collected.
		/// </summary>
		private readonly record struct FlushOutcome(bool Inserted, bool Intent);
	}
}
