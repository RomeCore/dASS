using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The SCM anchor: a frozen system prompt state (section states + rendered snapshot bytes)
	/// pinned to a chat message. Anchors are append-only: they are never mutated or removed.
	/// </summary>
	public class PromptStateAnchorMessageData : AdditionalChatData
	{
		/// <summary>
		/// The monotonic anchor ID (within the chat).
		/// </summary>
		public required int Id { get; init; }

		/// <summary>
		/// The ID of the agent this anchor belongs to.
		/// </summary>
		public required Guid AgentId { get; init; }

		/// <summary>
		/// The captured section states.
		/// </summary>
		public IReadOnlyList<PromptSectionStateBase> Sections { get; init; } = [];

		/// <summary>
		/// The frozen snapshot bytes: system prompt text and tool definitions.
		/// </summary>
		public required SystemPromptSnapshot Snapshot { get; init; }

		public PromptStateAnchorMessageData()
		{
			IsVisible = false;
		}
	}
}
