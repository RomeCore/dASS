using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Builds the model-facing content of a tool call: its result content plus the text of every
	/// <see cref="IContentChatAttachment"/> the tool or a stage wrote onto the call.
	/// </summary>
	/// <remarks>
	/// Pure and side-effect free, like <see cref="MessageContentProjector"/>, so the projection can be
	/// tested without the rendering pipeline. The composer skips it for a compacted call, whose content is
	/// replaced by a short marker.
	/// </remarks>
	public static class ToolCallContentProjector
	{
		/// <summary>
		/// Projects the call's content for the model.
		/// </summary>
		/// <param name="call">The call to project.</param>
		/// <returns>The result content with the content parts appended.</returns>
		public static string Project(ToolCall call)
		{
			var segments = new List<string?> { call.ResultContent };
			segments.AddRange(call.AdditionalData
				.OfType<IContentChatAttachment>()
				.Select(part => part.Content));

			return string.Join("\n\n", segments.Where(segment => !string.IsNullOrEmpty(segment)));
		}
	}
}
