using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils.Attachments;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The thin adapter the Lua tool surface (<c>dass.tool.result.*</c>) writes through. It replaces the
	/// deleted <c>ReactiveToolResult</c>: every write goes straight into the call, and only the members
	/// the call does not expose — the progress range and the script's own completion verdict — are kept
	/// here.
	/// </summary>
	internal sealed class LuaToolResult
	{
		private readonly IToolCall _call;
		private double? _progress;
		private double _progressMin;
		private double _progressMax = 1.0;
		private bool? _completed;

		/// <summary>
		/// Creates the adapter for one invocation.
		/// </summary>
		/// <param name="call">The call the script writes into.</param>
		public LuaToolResult(IToolCall call) => _call = call;

		/// <summary>The host the script attaches payloads to (the tool call itself).</summary>
		public IChatAttachmentHost Host => _call;

		/// <summary>The current accumulated output content.</summary>
		public string Content => _call.ResultContent ?? string.Empty;

		/// <summary>Whether the script has already completed the tool execution.</summary>
		public bool IsCompleted => _completed.HasValue;

		/// <summary>The script's verdict, or <see langword="null"/> while it has not completed the execution.</summary>
		public bool? Success => _completed;

		/// <summary>The current progress value, or <see langword="null"/> when progress is indeterminate.</summary>
		public double? Progress => _progress;

		/// <summary>The minimum progress value.</summary>
		public double ProgressMin => _progressMin;

		/// <summary>The maximum progress value.</summary>
		public double ProgressMax => _progressMax;

		/// <summary>The current status icon, or <see langword="null"/>.</summary>
		public VisualIconKind? StatusIcon => _call.StatusIcon;

		/// <summary>The current status title, or <see langword="null"/>.</summary>
		public string? StatusTitle => _call.StatusTitle;

		/// <summary>Whether the result content should be rendered as Markdown.</summary>
		public bool UseMarkdown
		{
			get => _call.UseMarkdown;
			set => _call.UseMarkdown = value;
		}

		/// <summary>
		/// Appends a chunk of output.
		/// </summary>
		/// <param name="line">The chunk to append.</param>
		public void Write(string line) => _call.AppendResultLine(line);

		/// <summary>
		/// Replaces the result content.
		/// </summary>
		/// <param name="text">The new content.</param>
		public void SetContent(string text) => _call.SetResult(text);

		/// <summary>
		/// Sets the progress together with its range.
		/// </summary>
		/// <param name="value">The current value, or <see langword="null"/> for indeterminate progress.</param>
		/// <param name="min">The minimum of the range, or <see langword="null"/> to keep the current one.</param>
		/// <param name="max">The maximum of the range, or <see langword="null"/> to keep the current one.</param>
		public void SetProgress(double? value, double? min = null, double? max = null)
		{
			_progress = value;
			if (min.HasValue)
				_progressMin = min.Value;
			if (max.HasValue)
				_progressMax = max.Value;

			_call.SetProgress(_progress, _progressMin, _progressMax);
		}

		/// <summary>
		/// Sets the status icon shown next to the call title.
		/// </summary>
		/// <param name="icon">The icon, or <see langword="null"/> to clear it.</param>
		public void SetStatusIcon(VisualIconKind? icon) => _call.SetStatusIcon(icon);

		/// <summary>
		/// Sets the status title shown next to the call title.
		/// </summary>
		/// <param name="title">The title, or <see langword="null"/> to clear it.</param>
		public void SetStatusTitle(string? title) => _call.SetStatusTitle(title);

		/// <summary>
		/// Sets the structured result consumed by external APIs.
		/// </summary>
		/// <param name="value">The structured result, or <see langword="null"/>.</param>
		public void SetStructured(JsonNode? value) => _call.SetStructuredResult(value);

		/// <summary>
		/// Attaches a payload (a UI part, a native attachment or a text contribution) to the call.
		/// </summary>
		/// <param name="attachment">The payload to attach.</param>
		public void AddAttachment(IChatAttachment attachment) => _call.AddAttachment(attachment);

		/// <summary>
		/// Clears the result content.
		/// </summary>
		public void Clear() => _call.SetResult(null);

		/// <summary>
		/// Records the script's verdict. The first call wins, matching the old completion semantics.
		/// </summary>
		/// <param name="success">Whether the tool execution should be reported as successful.</param>
		public void Complete(bool success) => _completed ??= success;

		/// <summary>
		/// Turns the recorded verdict into the main executor's return.
		/// </summary>
		/// <returns>The result of the execution.</returns>
		public ToolExecutionResult ToExecutionResult() =>
			_completed == true ? ToolExecutionResult.Succeeded : ToolExecutionResult.Failed;
	}
}
