using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// The effective chat context of an agent: what messages the agent can see and
	/// which checkpoints are applied to that message sequence.
	/// </summary>
	public class EffectiveChatContext
	{
		/// <summary>
		/// The agent descriptor for which this context is effective.
		/// </summary>
		public required ChatAgentDescriptor Agent { get; init; }

		/// <summary>
		/// The effective message sequence (ascending), including the pending assistant response.
		/// </summary>
		public required IReadOnlyList<EffectiveMessage> Messages { get; init; }

		/// <summary>
		/// Active checkpoints carried by the effective region with their relative indices.
		/// </summary>
		public required IReadOnlyList<EffectiveCheckpoint> Checkpoints { get; init; }

		/// <summary>
		/// The index of the first message in the effective history, relative to the <c>Chat.Messages</c>.
		/// </summary>
		public required int EffectiveMessagesStartIndex { get; init; }

		/// <summary>
		/// The cut boundary of the newest cut checkpoint expressed relative to <see cref="Messages"/>:
		/// all messages with index <c>&lt;= LastCutIndex</c> lie before the cut boundary
		/// (i.e. <c>Messages[LastCutIndex + 1]</c> is the first message after the newest cut).
		/// <c>-1</c> when there are no cuts, or when the cut leaves no visible message before it.
		/// The cut-ness logic (which checkpoint kinds are cuts) is computed by the effective context builder only.
		/// </summary>
		public required int LastCutIndex { get; init; }

		/// <summary>
		/// The index of the last checkpoint in the <see cref="Messages"/> list.
		/// This is useful for determining the most recent checkpoint that has been applied to the context.
		/// The default value is <c>-1</c> if there are no checkpoints.
		/// </summary>
		public required int LastCheckpointIndex { get; init; }
	}
}
