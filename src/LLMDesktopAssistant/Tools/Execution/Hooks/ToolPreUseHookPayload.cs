using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The document a <c>tool.pre-use</c> hook receives.
	/// </summary>
	/// <remarks>
	/// Only what a hook cannot reach on its own is carried: the argument set that will actually run, the
	/// provenance of the call and the behaviour the tool declared. Anything else the hook needs - the
	/// chat, the settings, the toolset - it resolves from the services it runs with.
	/// </remarks>
	public sealed class ToolPreUseHookPayload
	{
		/// <summary>
		/// The name the hook was matched against: the tool's origin and name, for example <c>native.fs-edit</c>.
		/// </summary>
		public string Tool { get; init; } = string.Empty;

		/// <summary>
		/// The behaviour flags of the call in kebab case, or <see langword="null"/> when the tool declares
		/// none. These are the descriptor's declarations, because the preview stage has not refined them
		/// yet at this point.
		/// </summary>
		public string[]? Behaviour { get; init; }

		/// <summary>
		/// The source that launched the invocation in kebab case: <c>chat-run</c>, <c>chat-restart</c>,
		/// <c>command</c> or <c>agent-task</c>. A hook that guards an agent decision and a user's own action
		/// differently branches on this.
		/// </summary>
		public string Source { get; init; } = string.Empty;

		/// <summary>
		/// The effective argument set - what the tool would actually run with, including an override a
		/// previous hook installed - not the model's raw intent.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// The chat the invocation belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The agent whose message owns the call, or <see langword="null"/> when the host is not an
		/// assistant message.
		/// </summary>
		public Guid? AgentId { get; init; }
	}
}
