using LiteDB;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// Represents a model for a tool call inside the database.
	/// </summary>
	public sealed class ToolCallModel
	{
		/// <summary>
		/// The unique identifier for the tool call.
		/// </summary>
		[BsonId]
		public int Id { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the order of the tool call.
		/// </summary>
		public int Order { get; set; }

		/// <summary>
		/// Gets or sets the message ID that this tool call belongs to.
		/// </summary>
		public int MessageId { get; set; }

		/// <summary>
		/// Gets or sets the tool name that being/been called.
		/// </summary>
		public string ToolName { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the title of the tool call.
		/// </summary>
		public LocaleKeyBase? Title { get; set; }

		/// <summary>
		/// Gets or sets the LLM API-specific tool call id that used by LLM to correctly bind each tool call to the tool message.
		/// </summary>
		public string ToolCallId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the function call arguments, this is JSON content serialized to string.
		/// </summary>
		public string FunctionArguments { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the result status of the tool call.
		/// </summary>
		public ToolStatusModel Status { get; set; }

		/// <summary>
		/// Gets or sets the status icon to be displayed. This will be shown next to the main title (that contains tool name).
		/// </summary>
		public VisualIconKind? StatusIcon { get; set; }

		/// <summary>
		/// Gets or sets the expected behaviour of the tool.
		/// </summary>
		public ToolBehaviour? ExpectedBehaviour { get; set; }

		/// <summary>
		/// Gets or sets the title of the status that will be shown next to the main title (that contains tool name).
		/// </summary>
		public string? StatusTitle { get; set; } = null;

		/// <summary>
		/// Gets or sets the result content of the tool call.
		/// </summary>
		public string? ResultContent { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the tool call result can be compacted.
		/// </summary>
		public bool CanBeCompacted { get; set; }

		/// <summary>
		/// Gets or sets whether to use markdown for rendering the result content.
		/// </summary>
		public bool UseMarkdown { get; set; }

		/// <summary>
		/// Gets or sets the result structured content of the tool call, this is a string converted from JSON.
		/// </summary>
		public string? StructuredResult { get; set; }
	}
}