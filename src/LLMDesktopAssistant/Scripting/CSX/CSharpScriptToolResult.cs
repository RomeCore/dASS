using System.Text.Json;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// Provides streaming result API for C# scripts: writing output lines, updating status and progress,
	/// setting structured results and completing the tool execution.
	/// </summary>
	/// <remarks>
	/// The adapter replaces the deleted <c>ReactiveToolResult</c>: every write goes straight into the
	/// call, and only the members the call does not expose — the progress range and the script's own
	/// completion verdict — are kept here.
	/// </remarks>
	public sealed class CSharpScriptToolResult
	{
		private readonly IToolCall _call;
		private double? _progress;
		private double _progressMin;
		private double _progressMax = 1.0;
		private bool? _completed;

		internal CSharpScriptToolResult(IToolCall call)
		{
			_call = call;
		}

		/// <summary>
		/// Gets the current accumulated output content.
		/// </summary>
		public string Content => _call.ResultContent ?? string.Empty;

		/// <summary>
		/// Gets whether the tool execution has already been completed.
		/// </summary>
		public bool IsCompleted => _completed.HasValue;

		/// <summary>
		/// Gets the completion status of the tool execution, or <see langword="null"/> if not completed yet.
		/// </summary>
		public bool? Success => _completed;

		/// <summary>
		/// Gets the current progress value, or <see langword="null"/> when progress is indeterminate.
		/// </summary>
		public double? Progress => _progress;

		/// <summary>
		/// Gets the minimum progress value.
		/// </summary>
		public double ProgressMin => _progressMin;

		/// <summary>
		/// Gets the maximum progress value.
		/// </summary>
		public double ProgressMax => _progressMax;

		/// <summary>
		/// Gets the current status icon name, or <see langword="null"/> if not set.
		/// </summary>
		public string? StatusIcon => _call.StatusIcon?.ToString();

		/// <summary>
		/// Gets the current status title, or <see langword="null"/> if not set.
		/// </summary>
		public string? StatusTitle => _call.StatusTitle;

		/// <summary>
		/// Gets or sets whether the result content should be rendered as Markdown.
		/// </summary>
		public bool UseMarkdown
		{
			get => _call.UseMarkdown;
			set => _call.UseMarkdown = value;
		}

		/// <summary>
		/// Appends a line of text to the result output.
		/// </summary>
		/// <param name="line">The line to append.</param>
		public void Write(string line)
		{
			_call.AppendResultLine(line);
		}

		/// <summary>
		/// Appends multiple lines to the result output at once.
		/// </summary>
		/// <param name="lines">The lines to append.</param>
		public void WriteLines(IEnumerable<string> lines)
		{
			foreach (var line in lines)
				_call.AppendResultLine(line);
		}

		/// <summary>
		/// Replaces the entire result content with the given text.
		/// </summary>
		/// <param name="text">The new content.</param>
		public void SetContent(string text)
		{
			_call.SetResult(text);
		}

		/// <summary>
		/// Sets the progress of the tool execution with optional range limits.
		/// </summary>
		/// <param name="value">The progress value, or <see langword="null"/> for indeterminate progress.</param>
		/// <param name="min">The minimum progress value. When <see langword="null"/>, the current minimum is kept.</param>
		/// <param name="max">The maximum progress value. When <see langword="null"/>, the current maximum is kept.</param>
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
		/// Sets the status icon and title shown in the UI next to the tool name.
		/// </summary>
		/// <param name="iconName">The icon name, either a bare material name (e.g. "File", "Web", "Check") or a full pack-prefixed name (e.g. "material:Check", "path:M0 0 L24 24"). Pass <see langword="null"/> or empty string to keep the current icon.</param>
		/// <param name="title">The status title text. Pass <see langword="null"/> or empty string to keep the current title.</param>
		public void SetStatus(string? iconName, string? title)
		{
			if (!string.IsNullOrEmpty(iconName))
			{
				var icon = VisualIconKind.Parse(iconName);
				_call.SetStatusIcon(VisualIconDataHandler.Resolve(icon).IsOk ? icon : null);
			}

			if (!string.IsNullOrEmpty(title))
				_call.SetStatusTitle(title);
		}

		/// <summary>
		/// Sets the structured result data that will be returned to the LLM alongside the text content.
		/// </summary>
		/// <param name="data">The structured data, or <see langword="null"/> to clear the structured result.</param>
		public void SetStructured(object? data)
		{
			_call.SetStructuredResult(data == null ? null : JsonSerializer.SerializeToNode(data));
		}

		/// <summary>
		/// Completes the tool execution. After calling this, further writes are ignored.
		/// </summary>
		/// <param name="success"><see langword="true"/> for success (default), <see langword="false"/> for error.</param>
		public void Complete(bool success = true)
		{
			_completed ??= success;
		}

		/// <summary>
		/// Completes the tool execution with a success status.
		/// </summary>
		public void CompleteWithSuccess()
		{
			_completed ??= true;
		}

		/// <summary>
		/// Completes the tool execution with an error status.
		/// </summary>
		public void CompleteWithError()
		{
			_completed ??= false;
		}

		/// <summary>
		/// Clears all result content lines.
		/// </summary>
		public void Clear()
		{
			_call.SetResult(null);
		}

		/// <summary>
		/// Turns the recorded verdict into the main executor's return.
		/// </summary>
		/// <returns>The result of the execution.</returns>
		internal ToolExecutionResult ToExecutionResult() =>
			_completed == true ? ToolExecutionResult.Succeeded : ToolExecutionResult.Failed;
	}
}
