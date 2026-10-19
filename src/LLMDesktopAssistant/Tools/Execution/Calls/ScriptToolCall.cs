using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Tools.Execution.Calls
{
	/// <summary>
	/// The call of a nested script invocation (<c>dass.tool.call</c>): in-memory, never persisted, and not
	/// projected into either world's message plumbing.
	/// </summary>
	/// <remarks>
	/// A script invocation has no owning message and no UI surface of its own, so the chat-native members
	/// (<see cref="LLM.Domain.ToolCall.AdditionalData"/>, <see cref="LLM.Domain.ToolCall.Title"/>) do not
	/// exist here. Attachments are collected in case a future script surface exposes them; today
	/// <c>dass.tool.call</c> returns content and the structured result only.
	/// </remarks>
	public sealed class ScriptToolCall : IToolCall
	{
		private readonly List<IChatAttachment> _attachments = [];
		private double? _progress;
		private double _minProgress;
		private double _maxProgress = 1.0;

		/// <inheritdoc/>
		public required string ToolName { get; init; }

		/// <inheritdoc/>
		public string Arguments { get; set; } = string.Empty;

		/// <inheritdoc/>
		public ToolStatus Status { get; set; } = ToolStatus.None;

		/// <inheritdoc/>
		public ToolBehaviour? ExpectedBehaviour { get; set; }

		/// <inheritdoc/>
		public string? ResultContent { get; set; }

		/// <inheritdoc/>
		public bool UseMarkdown { get; set; }

		/// <inheritdoc/>
		public JsonNode? StructuredResult { get; set; }

		/// <inheritdoc/>
		public VisualIconKind? StatusIcon { get; set; }

		/// <inheritdoc/>
		public string? StatusTitle { get; set; }

		/// <inheritdoc/>
		public CancellationTokenSource? CancellationTokenSource { get; set; }

		/// <summary>
		/// The payloads the tool added. Nothing consumes them yet; kept so a script-facing attachment
		/// surface does not need a domain change.
		/// </summary>
		public IReadOnlyList<IChatAttachment> Attachments => _attachments;

		/// <summary>
		/// The current progress value, or <see langword="null"/> for indeterminate progress.
		/// </summary>
		public double? Progress => _progress;

		/// <summary>
		/// The minimum of the progress range.
		/// </summary>
		public double MinProgress => _minProgress;

		/// <summary>
		/// The maximum of the progress range.
		/// </summary>
		public double MaxProgress => _maxProgress;

		/// <inheritdoc/>
		public void AddAttachment(IChatAttachment attachment) => _attachments.Add(attachment);

		/// <inheritdoc/>
		public bool RemoveAttachment(IChatAttachment attachment) => _attachments.Remove(attachment);

		/// <inheritdoc/>
		public void ClearAttachments() => _attachments.Clear();

		/// <inheritdoc/>
		public IEnumerable<IChatAttachment> GetAttachments() => _attachments;

		/// <inheritdoc/>
		public void SetProgress(double? value, double min, double max)
		{
			_minProgress = min;
			_maxProgress = max;
			_progress = value;
		}
	}
}
