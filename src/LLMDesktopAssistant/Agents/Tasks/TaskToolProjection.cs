using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// Projects chat tools into the task toolset.
	/// </summary>
	/// <remarks>
	/// A chat <see cref="ToolInfo"/> is already an <see cref="IToolDescriptor"/> and is used as is. Only a
	/// per-task approval override needs a copy: the toolset cache hands out frozen per-agent descriptors,
	/// so the change is applied to an unfrozen clone. The clone drops the chat-stamped context factory —
	/// the task entry point supplies <c>TaskToolContextFactory</c> in the invocation request and the
	/// dispatcher uses it verbatim.
	/// </remarks>
	internal static class TaskToolProjection
	{
		/// <summary>
		/// Returns the descriptor the task should use for a chat tool.
		/// </summary>
		/// <param name="tool">The chat tool.</param>
		/// <param name="approvalLevel">
		/// The approval level override, or <see langword="null"/> to keep the tool's own level (and the
		/// original instance).
		/// </param>
		/// <returns>The descriptor, possibly a frozen clone.</returns>
		public static IToolDescriptor Project(ToolInfo tool, ToolApprovalLevel? approvalLevel = null)
		{
			if (approvalLevel is null)
				return tool;

			var clone = tool.Clone();
			clone.ApprovalLevel = approvalLevel;
			clone.ContextFactory = null;
			clone.Freeze();
			return clone;
		}
	}
}
