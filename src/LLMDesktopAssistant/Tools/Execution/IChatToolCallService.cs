using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution
{
	/// <summary>
	/// The user's own actions on a single chat tool call: cancelling a call that is still running and re-running a
	/// finished one in place.
	/// </summary>
	/// <remarks>
	/// Both actions are the user's, so neither goes through the policy or the confirmation stage (spec §13). The
	/// service is chat-scoped: a restart needs the chat's context factory and the message that owns the call, and a
	/// cancellation only needs the call's own cancellation source.
	/// </remarks>
	public interface IChatToolCallService
	{
		/// <summary>
		/// Whether the call can be cancelled: it is not finished and its cancellation source is still live.
		/// </summary>
		/// <param name="call">The call to inspect.</param>
		bool CanCancel(IToolCall call);

		/// <summary>
		/// Cancels this call alone. The run and the sibling calls are untouched, and whatever the tool produced so far
		/// stays on the call.
		/// </summary>
		/// <param name="call">The call to cancel.</param>
		void Cancel(IToolCall call);

		/// <summary>
		/// Whether the call can be restarted: it is finished and its tool still exists in the chat.
		/// </summary>
		/// <param name="call">The call to inspect.</param>
		bool CanRestart(IToolCall call);

		/// <summary>
		/// Re-runs a finished call on the same <see cref="IToolCall"/> - status, result, progress and the payloads the
		/// tool attached are reset before the run, and the call keeps its identity so the prompt picks the new result
		/// up at the next request without a new turn.
		/// </summary>
		/// <param name="host">The message that owns the call.</param>
		/// <param name="call">The finished call to run again.</param>
		/// <param name="ct">Cancellation of the restart itself.</param>
		Task RestartAsync(ChatMessage host, IToolCall call, CancellationToken ct);
	}
}
