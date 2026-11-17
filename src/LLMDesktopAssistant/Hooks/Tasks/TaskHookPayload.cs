using LLMDesktopAssistant.Agents.Tasks;

namespace LLMDesktopAssistant.Hooks.Tasks
{
	/// <summary>
	/// The document both task hooks receive: one launch of one agentic task.
	/// </summary>
	/// <remarks>
	/// The shape is the same at the start and at the end, so one body can serve both events and compare the two:
	/// <see cref="TaskId"/> identifies the task across its launches, and <see cref="Launch"/> tells a continuation
	/// from a fresh task. Everything that only exists once the launch is over - <see cref="Status"/>,
	/// <see cref="BlockReason"/>, <see cref="Error"/>, <see cref="CompletedAt"/>, <see cref="DurationMs"/> - is
	/// <see langword="null"/> at <c>task.start</c>.
	/// <para/>
	/// <see cref="Usage"/> is cumulative over every launch of the task, while <see cref="NewUsage"/> covers only this
	/// one, which is what a cost tracker wants after a continuation: the earlier usage was already accounted for.
	/// </remarks>
	public sealed class TaskHookPayload
	{
		/// <summary>
		/// The identity of the task, stable across its launches.
		/// </summary>
		public Guid TaskId { get; init; }

		/// <summary>
		/// The task that launched this one, or <see langword="null"/> for a top-level task.
		/// </summary>
		public Guid? ParentTaskId { get; init; }

		/// <summary>
		/// The display name the launcher gave the task, or <see langword="null"/> when it gave none.
		/// </summary>
		public string? TaskName { get; init; }

		/// <summary>
		/// The launcher kind in kebab case, as it appears in the event name: <c>internal</c>, <c>tool</c>, ...
		/// </summary>
		public string Source { get; init; } = string.Empty;

		/// <summary>
		/// The free-form id of the launcher, as it appears in the target: <c>chat-agent-call</c>, <c>lua-api</c>, ...
		/// </summary>
		public string SourceId { get; init; } = string.Empty;

		/// <summary>
		/// The persisted chat the task belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// Whether the task is hidden from the message that triggered it and from the chat's task list.
		/// </summary>
		public bool Hidden { get; init; }

		/// <summary>
		/// The sender of the message that triggered the task, or <see langword="null"/> when no message did.
		/// </summary>
		public string? TriggeredSender { get; init; }

		/// <summary>
		/// The full name of the model the launch runs on.
		/// </summary>
		public string? Model { get; init; }

		/// <summary>
		/// The execution behaviour in kebab case: <c>normal</c>, <c>execute-once</c> or <c>only-response</c>.
		/// </summary>
		public string? Behaviour { get; init; }

		/// <summary>
		/// Which launch this is: <c>new</c> or <c>continue</c>, matching the event name.
		/// </summary>
		public string Launch { get; init; } = string.Empty;

		/// <summary>
		/// The terminal status in kebab case, or <see langword="null"/> while the task is running.
		/// </summary>
		/// <remarks>
		/// The words are <c>success</c>, <c>error</c>, <c>cancelled</c> and <c>blocked</c> - the last one for a launch
		/// that was blocked before it ran, and <c>error</c> for <see cref="AgentTaskStatus.Failed"/>.
		/// </remarks>
		public string? Status { get; init; }

		/// <summary>
		/// Why the launch was blocked before it ran, or <see langword="null"/> when it was not.
		/// </summary>
		public string? BlockReason { get; init; }

		/// <summary>
		/// The message of the exception that ended the launch, or <see langword="null"/> when none did.
		/// </summary>
		/// <remarks>
		/// A blocked launch reports its <see cref="BlockReason"/> here as well: that is the exception a waiting caller
		/// received, so a hook that watches every failure sees the refusal next to the failures.
		/// </remarks>
		public string? Error { get; init; }

		/// <summary>
		/// The type name of the exception that ended the launch, or <see langword="null"/> when none did.
		/// </summary>
		public string? ErrorType { get; init; }

		/// <summary>
		/// How many messages the task holds, initial ones included.
		/// </summary>
		public int MessageCount { get; init; }

		/// <summary>
		/// How many leading messages were supplied as the task's initial context.
		/// </summary>
		public int InitialMessageCount { get; init; }

		/// <summary>
		/// How many times the task continued after a response without tool calls, through the feedback function.
		/// </summary>
		public int FeedbackIterations { get; init; }

		/// <summary>
		/// How many sub-tasks the task launched.
		/// </summary>
		public int SubTaskCount { get; init; }

		/// <summary>
		/// The last content the agent generated, or <see langword="null"/> when it generated none.
		/// </summary>
		public string? LastGeneratedContent { get; init; }

		/// <summary>
		/// The usage of every launch of the task so far, or <see langword="null"/> when nothing ran yet.
		/// </summary>
		public AgentUsageStatistics? Usage { get; init; }

		/// <summary>
		/// The usage of this launch only, or <see langword="null"/> when nothing ran yet.
		/// </summary>
		public AgentUsageStatistics? NewUsage { get; init; }

		/// <summary>
		/// When this launch started.
		/// </summary>
		public DateTime StartedAt { get; init; }

		/// <summary>
		/// When this launch ended, or <see langword="null"/> while it is running.
		/// </summary>
		public DateTime? CompletedAt { get; init; }

		/// <summary>
		/// How long this launch took in milliseconds, or <see langword="null"/> while it is running.
		/// </summary>
		public double? DurationMs { get; init; }
	}
}
