namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// What a <c>tool.result.*</c> hook returns: the text the model reads instead of the one the call
	/// produced.
	/// </summary>
	public sealed class ToolResultHookVerdict
	{
		/// <summary>
		/// The text that replaces the call's result, or <see langword="null"/> to keep it. An empty or
		/// whitespace-only value is ignored, so the model never loses the outcome it was told about.
		/// </summary>
		public string? Result { get; init; }

		/// <summary>
		/// Whether the replacement is Markdown, or <see langword="null"/> to keep the call's own flag.
		/// </summary>
		public bool? Markdown { get; init; }
	}
}
