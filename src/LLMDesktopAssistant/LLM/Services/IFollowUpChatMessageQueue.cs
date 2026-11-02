using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The single owner of a chat's follow-up queue: the only reader and writer of
	/// <see cref="MVVM.Additional.FollowUp.FollowUpChatMessagesData"/>.
	/// <para/>
	/// It holds data and policy only - ordering, the gate level, and the in-flight mark that makes one entry belong
	/// to exactly one turn at a time. It knows nothing about the history and nothing about generation; the drain
	/// itself lives in the turn owner.
	/// </summary>
	public interface IFollowUpChatMessageQueue
	{
		/// <summary>
		/// The number of pending entries. Reading never creates the underlying chat item.
		/// </summary>
		int Count { get; }

		/// <summary>
		/// Gets a value indicating whether there is at least one pending entry.
		/// </summary>
		bool HasAny { get; }

		/// <summary>
		/// The pending entries in composition order, as a snapshot for the queue dialog.
		/// </summary>
		IReadOnlyList<FollowUpMessage> Entries { get; }

		/// <summary>
		/// The level the next enqueued message gets. Persisted with the queue, so the last picked value survives a
		/// restart.
		/// </summary>
		FollowUpInjectionLevel DefaultLevel { get; set; }

		/// <summary>
		/// The earliest level among the pending entries, or <see langword="null"/> when the queue is empty. The whole
		/// queue is drained at the first barrier that reaches it.
		/// </summary>
		FollowUpInjectionLevel? MinLevel { get; }

		/// <summary>
		/// Gets a value indicating whether the queue may be drained at the given barrier: it must be non-empty and its
		/// gate must not be later than the barrier.
		/// </summary>
		/// <param name="barrier">The barrier the execution loop has reached.</param>
		bool HasEligible(FollowUpInjectionLevel barrier);

		/// <summary>
		/// Adds an entry at the tail, copying the input's parts into it.
		/// </summary>
		/// <param name="input">The composed message.</param>
		/// <param name="level">The level the entry is enqueued with.</param>
		/// <param name="generateIntent">Whether the message asked for an answer.</param>
		/// <param name="canExecuteCommand">Whether the message may run a slash command.</param>
		FollowUpMessage Enqueue(UserInput input, FollowUpInjectionLevel level, bool generateIntent, bool canExecuteCommand);

		/// <summary>
		/// Takes the head of the queue and marks it in flight for this turn. Returns <see langword="false"/> when the
		/// queue is empty or when another turn already owns the head - a second drainer skips it instead of inserting
		/// the same message twice.
		/// </summary>
		/// <param name="entry">The head entry, when this call owns it.</param>
		bool TryBeginNext(out FollowUpMessage entry);

		/// <summary>
		/// Removes an in-flight entry: its message has reached the history. This is the point where a follow-up is
		/// eaten - never before, so a crash can only duplicate a pending entry, never lose one.
		/// </summary>
		/// <param name="entry">The entry the caller owns.</param>
		void Complete(FollowUpMessage entry);

		/// <summary>
		/// Releases an in-flight entry without removing it, for the case where nothing reached the history.
		/// </summary>
		/// <param name="entry">The entry the caller owns.</param>
		void Abandon(FollowUpMessage entry);

		/// <summary>
		/// Removes a pending entry outright, for the queue dialog.
		/// </summary>
		/// <param name="entry">The entry to drop.</param>
		void Remove(FollowUpMessage entry);

		/// <summary>
		/// Drops every pending entry.
		/// </summary>
		void Clear();

		/// <summary>
		/// Raised after any change of the queue, so the tab strip and the dialog can follow it.
		/// </summary>
		event Action? Changed;
	}
}
