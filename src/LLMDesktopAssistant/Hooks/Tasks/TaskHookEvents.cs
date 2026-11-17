using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Hooks.Tasks
{
	/// <summary>
	/// The event vocabulary of the agentic-task pipeline: the names a hook matches with <c>hook-type</c>, and the
	/// target it matches with <c>hook-target</c>.
	/// </summary>
	/// <remarks>
	/// The name is composed as <c>task.&lt;phase&gt;.&lt;source&gt;.&lt;variant&gt;</c> - the shape the tool pipeline uses -
	/// where the source is the launcher kind and the variant is the entry (a launch or a continuation) or the
	/// terminal status. The target is the free-form launch source id and the task name, so one launcher is matched
	/// with a glob - <c>agent-callsub.*</c>, <c>chat-naming.*</c> - while the name stays readable in a log.
	/// <para/>
	/// Only a task launched with a chat fires these events: the dispatcher is a chat service, so a task that names no
	/// chat has no hook surface at all.
	/// </remarks>
	public static class TaskHookEvents
	{
		/// <summary>
		/// The base of the event fired once per launch of an agentic task:
		/// <c>task.start.{source}.{new|continue}</c>.
		/// </summary>
		public const string Start = "task.start";

		/// <summary>
		/// The base of the event fired when a launch of an agentic task reaches a terminal status:
		/// <c>task.stop.{source}.{success|error|cancelled|blocked}</c>.
		/// </summary>
		public const string Stop = "task.stop";

		/// <summary>
		/// The name a task without a <c>TaskName</c> contributes to the target.
		/// </summary>
		public const string UnknownName = "unknown";

		/// <summary>
		/// Composes the event fired when an agentic task starts running.
		/// </summary>
		/// <param name="source">The launcher kind.</param>
		/// <param name="isContinue">Whether the launch continues a finished task instead of creating a new one.</param>
		/// <returns>For example <c>task.start.tool.continue</c>.</returns>
		public static string ComposeStart(AgentTaskLaunchSource source, bool isContinue) =>
			Start + "." + DescribeSource(source) + "." + (isContinue ? "continue" : "new");

		/// <summary>
		/// Composes the event fired when a launch of an agentic task ends.
		/// </summary>
		/// <param name="source">The launcher kind.</param>
		/// <param name="status">The terminal status of the launch.</param>
		/// <returns>For example <c>task.stop.tool.error</c>.</returns>
		public static string ComposeStop(AgentTaskLaunchSource source, AgentTaskStatus status) =>
			Stop + "." + DescribeSource(source) + "." + DescribeStatus(status);

		/// <summary>
		/// Composes the target a hook matches with <c>hook-target</c>: the launch source id and the task name.
		/// </summary>
		/// <param name="sourceId">The free-form id the launcher reports.</param>
		/// <param name="taskName">The display name of the task, or <see langword="null"/> when it has none.</param>
		/// <returns>For example <c>agent-callsub.web-searcher</c>, or <c>chat-naming.unknown</c>.</returns>
		public static string ComposeTarget(string sourceId, string? taskName) =>
			sourceId + "." + (string.IsNullOrWhiteSpace(taskName) ? UnknownName : taskName);

		/// <summary>
		/// Renders the launcher kind in kebab case.
		/// </summary>
		/// <param name="source">The launcher kind.</param>
		/// <returns>For example <c>script</c>.</returns>
		public static string DescribeSource(AgentTaskLaunchSource source) =>
			KebabEnumNames<AgentTaskLaunchSource>.Serialize(source) ?? source.ToString().ToLowerInvariant();

		/// <summary>
		/// Renders the terminal status in kebab case.
		/// </summary>
		/// <remarks>
		/// The stop word does not follow the enum name everywhere: <see cref="AgentTaskStatus.Failed"/> is spelled
		/// <c>error</c>, matching the rest of the hook vocabulary. A hook written against <c>task.stop.*.failed</c>
		/// loads and never fires.
		/// </remarks>
		/// <param name="status">The terminal status of the launch.</param>
		/// <returns>For example <c>error</c>.</returns>
		public static string DescribeStatus(AgentTaskStatus status) => status switch
		{
			AgentTaskStatus.Success => "success",
			AgentTaskStatus.Failed => "error",
			AgentTaskStatus.Cancelled => "cancelled",
			AgentTaskStatus.Blocked => "blocked",
			_ => KebabEnumNames<AgentTaskStatus>.Serialize(status) ?? status.ToString().ToLowerInvariant()
		};
	}
}
