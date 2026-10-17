using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Support
{
	/// <summary>
	/// The convenience surface tools write their output through. It mirrors the names the old reactive
	/// result used, so a tool migration stays mechanical instead of turning into a rewrite.
	/// </summary>
	/// <remarks>
	/// There is deliberately no completion method: the main executor's task <b>is</b> the completion.
	/// Every method here mutates the call, which is the same object the UI binds to.
	/// </remarks>
	public static class ToolCallExtensions
	{
		/// <summary>
		/// Sets the status icon shown next to the call title.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="icon">The icon, or <see langword="null"/> to clear it.</param>
		public static void SetStatusIcon(this IToolCall call, VisualIconKind? icon) => call.StatusIcon = icon;

		/// <summary>
		/// Sets the status title shown next to the call title.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="title">The title, or <see langword="null"/> to clear it.</param>
		public static void SetStatusTitle(this IToolCall call, string? title) => call.StatusTitle = title;

		/// <summary>
		/// Sets the status icon and title together.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="icon">The icon, or <see langword="null"/>.</param>
		/// <param name="title">The title, or <see langword="null"/>.</param>
		public static void SetStatus(this IToolCall call, VisualIconKind? icon, string? title)
		{
			call.StatusIcon = icon;
			call.StatusTitle = title;
		}

		/// <summary>
		/// Reports progress on the default 0..1 range.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="value">The current value, or <see langword="null"/> for indeterminate progress.</param>
		public static void SetProgress(this IToolCall call, double? value) => call.SetProgress(value, 0.0, 1.0);

		/// <summary>
		/// Replaces the result content.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="content">The new content, or <see langword="null"/>.</param>
		public static void SetResult(this IToolCall call, string? content) => call.ResultContent = content;

		/// <summary>
		/// Appends one chunk of output as a new line, keeping the existing content intact. This is the
		/// replacement for adding a line to the old reactive collection.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="line">The chunk to append.</param>
		public static void AppendResultLine(this IToolCall call, string line)
		{
			call.ResultContent = string.IsNullOrEmpty(call.ResultContent)
				? line
				: call.ResultContent + Environment.NewLine + line;
		}

		/// <summary>
		/// Splits the current result content into lines. Lossy for a chunk that itself contained line
		/// breaks; used only where the old code consumed the reactive line collection.
		/// </summary>
		/// <param name="call">The call to read.</param>
		/// <returns>The lines, or an empty array when there is no content.</returns>
		public static string[] GetResultLines(this IToolCall call) =>
			string.IsNullOrEmpty(call.ResultContent)
				? []
				: call.ResultContent.Split(Environment.NewLine);

		/// <summary>
		/// Sets the structured result consumed by external APIs.
		/// </summary>
		/// <param name="call">The call to update.</param>
		/// <param name="value">The structured result, or <see langword="null"/>.</param>
		public static void SetStructuredResult(this IToolCall call, JsonNode? value) => call.StructuredResult = value;

		/// <summary>
		/// Marks the call as successful. The settlement stage still finalizes the content.
		/// </summary>
		/// <param name="call">The call to update.</param>
		public static void MarkSuccess(this IToolCall call) => call.Status = ToolStatus.Success;

		/// <summary>
		/// Marks the call as failed. The settlement stage still finalizes the content.
		/// </summary>
		/// <param name="call">The call to update.</param>
		public static void MarkFailed(this IToolCall call) => call.Status = ToolStatus.Error;

		/// <summary>
		/// Marks the call as cancelled. Produced output is kept.
		/// </summary>
		/// <param name="call">The call to update.</param>
		public static void MarkCancelled(this IToolCall call) => call.Status = ToolStatus.Cancelled;
	}
}
