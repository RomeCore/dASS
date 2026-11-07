using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// The document a <c>tool.result.*</c> hook receives: what the call ended with, and the text the model
	/// would read right now.
	/// </summary>
	/// <remarks>
	/// The outcome is already in the event name, and it is repeated in <see cref="Status"/> because a hook
	/// that matches the whole family (<c>tool.result.*</c>) needs it in the document.
	/// <see cref="Error"/> and <see cref="ErrorType"/> are filled only when the call threw, which is the
	/// <c>error</c> outcome; every other outcome leaves them <see langword="null"/>.
	/// </remarks>
	public sealed class ToolResultHookPayload
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
		/// The terminal status in kebab case: <c>success</c>, <c>error</c>, <c>cancelled</c> or <c>rejected</c>.
		/// </summary>
		public string Status { get; init; } = string.Empty;

		/// <summary>
		/// The effective argument set the call ran with, including an override a pre-use hook installed.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// The behaviour flags of the call in kebab case, or <see langword="null"/> when none are declared.
		/// </summary>
		public string[]? Behaviour { get; init; }

		/// <summary>
		/// The text the model will read, or <see langword="null"/> when nothing was produced.
		/// </summary>
		public string? Result { get; init; }

		/// <summary>
		/// Whether <see cref="Result"/> is Markdown.
		/// </summary>
		public bool Markdown { get; init; }

		/// <summary>
		/// The exception message, when the call ended by throwing; otherwise <see langword="null"/>.
		/// </summary>
		public string? Error { get; init; }

		/// <summary>
		/// The exception type name, when the call ended by throwing; otherwise <see langword="null"/>.
		/// </summary>
		public string? ErrorType { get; init; }

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
