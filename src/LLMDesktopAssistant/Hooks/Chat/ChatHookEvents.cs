using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The event vocabulary of the chat pipeline: the names a hook matches with <c>hook-type</c>, and the target it
	/// matches with <c>hook-target</c>.
	/// </summary>
	/// <remarks>
	/// The name is composed as <c>&lt;area&gt;.&lt;phase&gt;.&lt;variant&gt;</c>, the same shape the tool pipeline uses.
	/// The target of a user-message event is the sender as <c>&lt;id&gt;.&lt;name&gt;</c>, so one user can be matched with
	/// a glob - <c>admin.Admin</c>, <c>admin.*</c> or a <c>*.Admin</c>.
	/// </remarks>
	public static class ChatHookEvents
	{
		/// <summary>
		/// The base of the event fired when a user sends a message: <c>message.user.submit.{direct|queue|edit}</c>.
		/// </summary>
		public const string UserSubmit = "message.user.submit";

		/// <summary>
		/// The base of the event fired around a user message write: <c>message.user.insert.{pre|post}</c>.
		/// </summary>
		public const string UserInsert = "message.user.insert";

		/// <summary>
		/// The event fired when a pending assistant message has just been created, before its request is assembled.
		/// </summary>
		public const string AssistantStart = "message.assistant.start";

		/// <summary>
		/// The base of the event fired when an assistant message reaches a terminal status:
		/// <c>message.assistant.stop.{success|error|cancelled}</c>.
		/// </summary>
		public const string AssistantStop = "message.assistant.stop";

		/// <summary>
		/// The event fired when the multi-agent turn is about to start, before any agent is selected.
		/// </summary>
		public const string TurnBegin = "chat.turn.begin";

		/// <summary>
		/// The event fired when the ordering service has to choose the next agent of the turn.
		/// </summary>
		public const string TurnAgentBegin = "chat.turn.agent.begin";

		/// <summary>
		/// The event fired when an agent runs again without a fresh selection: a resume or a tool cycle.
		/// </summary>
		public const string TurnAgentContinue = "chat.turn.agent.continue";

		/// <summary>
		/// The base of the event fired when an agent's turn ends:
		/// <c>chat.turn.agent.stop.{success|error|cancelled}.{complete|interrupted}</c>.
		/// </summary>
		public const string TurnAgentStop = "chat.turn.agent.stop";

		/// <summary>
		/// The base of the event fired when the multi-agent turn ends: <c>chat.turn.stop.{complete|interrupted|cancelled}</c>.
		/// </summary>
		public const string TurnStop = "chat.turn.stop";

		/// <summary>
		/// Composes the event fired when the user starts a send.
		/// </summary>
		/// <param name="kind">The user action.</param>
		/// <returns>For example <c>message.user.submit.queue</c>.</returns>
		public static string ComposeUserSubmit(UserSubmitKind kind) =>
			UserSubmit + "." + KebabEnumNames<UserSubmitKind>.Serialize(kind);

		/// <summary>
		/// Composes the event fired around a user message write.
		/// </summary>
		/// <param name="phase">The moment of the write.</param>
		/// <returns>For example <c>message.user.insert.pre</c>.</returns>
		public static string ComposeUserInsert(UserInsertPhase phase) =>
			UserInsert + "." + KebabEnumNames<UserInsertPhase>.Serialize(phase);

		/// <summary>
		/// Composes the event fired when an assistant message reaches a terminal status.
		/// </summary>
		/// <param name="status">The status the message ended with.</param>
		/// <returns>For example <c>message.assistant.stop.cancelled</c>.</returns>
		public static string ComposeAssistantStop(MessageStopStatus status) =>
			AssistantStop + "." + KebabEnumNames<MessageStopStatus>.Serialize(status);

		/// <summary>
		/// Composes the event fired when an agent's turn ends.
		/// </summary>
		/// <param name="status">The status of the agent's last message.</param>
		/// <param name="reason">Why the agent's turn ended.</param>
		/// <returns>For example <c>chat.turn.agent.stop.success.interrupted</c>.</returns>
		public static string ComposeTurnAgentStop(MessageStopStatus status, AgentStopReason reason) =>
			TurnAgentStop + "." + KebabEnumNames<MessageStopStatus>.Serialize(status) + "." +
			KebabEnumNames<AgentStopReason>.Serialize(reason);

		/// <summary>
		/// Composes the event fired when the multi-agent turn ends.
		/// </summary>
		/// <param name="reason">Why the turn ended.</param>
		/// <returns>For example <c>chat.turn.stop.interrupted</c>.</returns>
		public static string ComposeTurnStop(TurnStopReason reason) =>
			TurnStop + "." + KebabEnumNames<TurnStopReason>.Serialize(reason);

		/// <summary>
		/// Composes the target a hook matches with <c>hook-target</c>: the sender id and its display name.
		/// </summary>
		/// <param name="senderId">The login of the user.</param>
		/// <param name="sender">The display name, or <see langword="null"/> when the login does not resolve.</param>
		/// <returns>For example <c>admin.Admin</c>.</returns>
		public static string ComposeUserTarget(string senderId, string? sender) =>
			ComposeTarget(senderId, sender);

		/// <summary>
		/// Composes the target of an assistant-message event: the agent id and its name.
		/// </summary>
		/// <param name="agentId">The id of the agent.</param>
		/// <param name="agent">The name of the agent.</param>
		/// <returns>For example <c>3f2a...-c1.Researcher</c>.</returns>
		public static string ComposeAgentTarget(Guid agentId, string agent) =>
			ComposeTarget(agentId.ToString(), agent);

		private static string ComposeTarget(string id, string? name) =>
			string.IsNullOrEmpty(name) ? id : id + "." + name;
	}
}
