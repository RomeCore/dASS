using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Tools.Execution.Support
{
	/// <summary>
	/// Builds the model-facing content of a tool call: its result content plus the text of every
	/// <see cref="IContentChatAttachment"/> the tool or a stage wrote onto the call.
	/// </summary>
	/// <remarks>
	/// The two worlds store their payloads differently - a chat call keeps them in the message part
	/// collection, a task call in its own attachment list - so the helper takes the collection instead of
	/// reading it from <see cref="Domain.IToolCall"/>. The separator is fixed here: the chat prompt already
	/// emits it, and changing it would rewrite a byte-stable SCM prefix.
	/// </remarks>
	public static class ToolCallContentBuilder
	{
		/// <summary>The separator between the result content and a content part.</summary>
		public const string Separator = "\n\n";

		/// <summary>
		/// Builds the model-facing content of a call.
		/// </summary>
		/// <param name="resultContent">The content the tool produced, or <see langword="null"/>.</param>
		/// <param name="payloads">Everything the call carries; only the content branch contributes text.</param>
		/// <returns>The result content with the content parts appended.</returns>
		public static string Build(string? resultContent, IEnumerable<IChatAttachment> payloads)
		{
			var segments = new List<string?> { resultContent };
			segments.AddRange(payloads.OfType<IContentChatAttachment>().Select(part => part.Content));

			return string.Join(Separator, segments.Where(segment => !string.IsNullOrEmpty(segment)));
		}
	}
}
