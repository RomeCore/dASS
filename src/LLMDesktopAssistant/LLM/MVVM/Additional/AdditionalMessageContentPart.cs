namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// A message part that contributes text to the <em>model-facing</em> content of a message rather than to the UI
	/// alone. Its <see cref="Content"/> is appended to the message content by the prompt message renderer whenever the
	/// content part facet is requested, while the part itself renders as a chip.
	/// </summary>
	/// <remarks>
	/// A slash command uses it to inject a skill body, or a sub-agent's result, into the message it produced. The part
	/// is persisted with the message, so a reload keeps the injected text in the prompt.
	/// </remarks>
	public class AdditionalMessageContentPart : AdditionalMessagePart
	{
		private string _content = string.Empty;
		/// <summary>
		/// Gets or sets the text contributed to the model-facing message content.
		/// </summary>
		public string Content
		{
			get => _content;
			set => SetProperty(ref _content, value);
		}
	}
}
