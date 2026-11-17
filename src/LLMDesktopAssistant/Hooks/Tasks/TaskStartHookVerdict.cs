using LLMDesktopAssistant.Hooks.Chat;

namespace LLMDesktopAssistant.Hooks.Tasks
{
	/// <summary>
	/// What a <c>task.start.{source}.{new|continue}</c> hook returns: its opinion about the launch and, optionally,
	/// the reason the caller is shown.
	/// </summary>
	/// <remarks>
	/// A refusal is the only thing a task hook can do beyond observing: the parameters of a launch - model, tools,
	/// initial messages - are not negotiable, so a start hook either lets the task run or stops it before its first
	/// request. The verdict is a chain value, and the chain replaces rather than merges: each sequential hook gets
	/// the previous verdict as <c>previous</c> and its return value becomes the new one in full.
	/// </remarks>
	public sealed class TaskStartHookVerdict
	{
		/// <summary>
		/// The hook's opinion, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ChatHookDecision? Decision { get; init; }

		/// <summary>
		/// Why the hook refused. It becomes the error the awaiting caller reads and the task's block reason; an empty
		/// reason falls back to a generic one. Ignored when the decision is not a refusal.
		/// </summary>
		public string? Reason { get; init; }
	}
}
