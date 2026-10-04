using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// An immutable snapshot of a <see cref="MemoryBlockAttachment"/> resolved for an agent task.
	/// Unlike the attachment, it holds the <see cref="MemoryBlock"/> directly and captures read/write
	/// access at task launch time, so the task is not affected by later settings changes.
	/// </summary>
	public sealed class TaskMemoryBlock
	{
		/// <summary>
		/// Gets the memory block this snapshot points to.
		/// </summary>
		public required MemoryBlock Block { get; init; }

		/// <summary>
		/// Gets a value indicating whether the task can read from the block.
		/// </summary>
		public required bool CanRead { get; init; }

		/// <summary>
		/// Gets a value indicating whether the task can write to the block.
		/// </summary>
		public required bool CanWrite { get; init; }

		/// <summary>
		/// Creates a snapshot from the specified memory block attachment.
		/// </summary>
		/// <param name="attachment">The attachment to snapshot.</param>
		/// <returns>The created snapshot.</returns>
		/// <exception cref="ArgumentException">Thrown when the attachment has no resolved memory block.</exception>
		public static TaskMemoryBlock FromAttachment(MemoryBlockAttachment attachment)
		{
			if (attachment.Reference.Object is null)
				throw new ArgumentException("The attachment has no resolved memory block.", nameof(attachment));

			return new TaskMemoryBlock
			{
				Block = attachment.Reference.Object,
				CanRead = attachment.AllowsReading(),
				CanWrite = attachment.AllowsWriting()
			};
		}

		/// <summary>
		/// Resolves the memory block snapshots available to the specified agent in the specified chat.
		/// Returns an empty list when memory is disabled for the chat or the agent.
		/// </summary>
		/// <param name="chat">The chat providing the memory settings.</param>
		/// <param name="agent">The agent whose memory attachments should be resolved.</param>
		/// <returns>The resolved immutable list of snapshots.</returns>
		public static ImmutableList<TaskMemoryBlock> ResolveBlocks(Chat chat, ChatAgentDescriptor? agent)
		{
			// TODO: This thing will be removed when memory blocks become addons
			var memoryOptions = chat.Services.GetRequiredService<IChatSettingsService>().Settings.Memory.GetEffectiveMemoryOptions();
			if (!memoryOptions.EnableMemory || !memoryOptions.ManualControlEnabled || agent is null || !agent.Memory.EnableMemory)
				return [];

			return agent.Memory.GetEnabledBlocks(chat.Services.GetRequiredService<IChatSettingsService>().Settings)
				.Select(b => FromAttachment(b.Attachment))
				.ToImmutableList();
		}
	}
}
