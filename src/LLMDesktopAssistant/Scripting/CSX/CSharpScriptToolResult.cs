using System.Text.Json;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// Provides streaming result API for C# scripts: writing output lines, updating status and progress,
	/// setting structured results and completing the tool execution.
	/// </summary>
	public sealed class CSharpScriptToolResult
	{
		private readonly ReactiveToolResult _result;

		internal CSharpScriptToolResult(ReactiveToolResult result)
		{
			_result = result;
		}

		/// <summary>
		/// Gets the current accumulated output content.
		/// </summary>
		public string Content => _result.ResultContent;

		/// <summary>
		/// Gets whether the tool execution has already been completed.
		/// </summary>
		public bool IsCompleted => _result.Completion.IsCompleted;

		/// <summary>
		/// Gets the completion status of the tool execution, or <see langword="null"/> if not completed yet.
		/// </summary>
		public bool? Success => _result.Completion.IsCompleted ? _result.Completion.Result : null;

		/// <summary>
		/// Gets the current progress value, or <see langword="null"/> when progress is indeterminate.
		/// </summary>
		public double? Progress => _result.Progress;

		/// <summary>
		/// Gets the minimum progress value.
		/// </summary>
		public double ProgressMin => _result.MinProgress;

		/// <summary>
		/// Gets the maximum progress value.
		/// </summary>
		public double ProgressMax => _result.MaxProgress;

		/// <summary>
		/// Gets the current status icon name, or <see langword="null"/> if not set.
		/// </summary>
		public string? StatusIcon => _result.StatusIcon?.ToString();

		/// <summary>
		/// Gets the current status title, or <see langword="null"/> if not set.
		/// </summary>
		public string? StatusTitle => _result.StatusTitle;

		/// <summary>
		/// Gets or sets whether the result content should be rendered as Markdown.
		/// </summary>
		public bool UseMarkdown
		{
			get => _result.UseMarkdown;
			set => _result.UseMarkdown = value;
		}

		/// <summary>
		/// Appends a line of text to the result output.
		/// </summary>
		/// <param name="line">The line to append.</param>
		public void Write(string line)
		{
			_result.ResultContentLines.Add(line);
		}

		/// <summary>
		/// Appends multiple lines to the result output at once.
		/// </summary>
		/// <param name="lines">The lines to append.</param>
		public void WriteLines(IEnumerable<string> lines)
		{
			foreach (var line in lines)
				_result.ResultContentLines.Add(line);
		}

		/// <summary>
		/// Replaces the entire result content with the given text.
		/// </summary>
		/// <param name="text">The new content.</param>
		public void SetContent(string text)
		{
			_result.ResultContent = text;
		}

		/// <summary>
		/// Sets the progress of the tool execution with optional range limits.
		/// </summary>
		/// <param name="value">The progress value, or <see langword="null"/> for indeterminate progress.</param>
		/// <param name="min">The minimum progress value. When <see langword="null"/>, the current minimum is kept.</param>
		/// <param name="max">The maximum progress value. When <see langword="null"/>, the current maximum is kept.</param>
		public void SetProgress(double? value, double? min = null, double? max = null)
		{
			_result.Progress = value;
			if (min.HasValue)
				_result.MinProgress = min.Value;
			if (max.HasValue)
				_result.MaxProgress = max.Value;
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
				_result.StatusIcon = VisualIconDataHandler.Resolve(icon).IsOk ? icon : null;
			}

			if (!string.IsNullOrEmpty(title))
				_result.StatusTitle = title;
		}

		/// <summary>
		/// Sets the structured result data that will be returned to the LLM alongside the text content.
		/// </summary>
		/// <param name="data">The structured data, or <see langword="null"/> to clear the structured result.</param>
		public void SetStructured(object? data)
		{
			_result.StructuredResult = data == null ? null : JsonSerializer.SerializeToNode(data);
		}

		/// <summary>
		/// Completes the tool execution. After calling this, further writes are ignored.
		/// </summary>
		/// <param name="success"><see langword="true"/> for success (default), <see langword="false"/> for error.</param>
		public void Complete(bool success = true)
		{
			_result.TryComplete(success);
		}

		/// <summary>
		/// Completes the tool execution with a success status.
		/// </summary>
		public void CompleteWithSuccess()
		{
			_result.TryCompleteWithSuccess();
		}

		/// <summary>
		/// Completes the tool execution with an error status.
		/// </summary>
		public void CompleteWithError()
		{
			_result.TryCompleteWithError();
		}

		/// <summary>
		/// Clears all result content lines.
		/// </summary>
		public void Clear()
		{
			_result.ResultContentLines.Clear();
		}
	}
}
