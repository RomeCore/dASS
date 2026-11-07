using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The document a <c>tool.confirm.{source}.{pending}</c> hook receives.
	/// </summary>
	/// <remarks>
	/// The event and its origin are not repeated: both are in <c>hook_type</c> and <c>hook_name</c>.
	/// <see cref="Tool"/> carries the bare name, which the origin-qualified <c>hook_name</c> cannot give
	/// back without parsing.
	/// </remarks>
	public sealed class ToolConfirmHookPayload
	{
		/// <summary>
		/// The bare tool name, for example <c>fs-edit</c>.
		/// </summary>
		public string Tool { get; init; } = string.Empty;

		/// <summary>
		/// The invocation source in kebab case: <c>chat-run</c>, <c>chat-restart</c>, <c>command</c>, <c>script</c> or
		/// <c>agent-task</c>.
		/// </summary>
		public string Source { get; init; } = string.Empty;

		/// <summary>
		/// The decision the pipeline arrived with, in kebab case: <c>approve</c>, <c>ask</c> or <c>disallow</c>.
		/// </summary>
		public string Decision { get; init; } = string.Empty;

		/// <summary>
		/// The layer that produced <see cref="Decision"/>, in kebab case, or <see langword="null"/>.
		/// </summary>
		public string? ReasonSource { get; init; }

		/// <summary>
		/// The explanation that came with the decision, or <see langword="null"/>.
		/// </summary>
		public string? Reason { get; init; }

		/// <summary>
		/// The effective argument set the call is about to run with.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// The behaviour flags of the call in kebab case, or <see langword="null"/> when none are declared.
		/// </summary>
		public string[]? Behaviour { get; init; }

		/// <summary>
		/// The chat the invocation belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The agent whose message owns the call, or <see langword="null"/>.
		/// </summary>
		public Guid? AgentId { get; init; }
	}
}
