using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional.Preview;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;

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
	public class AdditionalMessageContentPart : AdditionalMessagePart, IContentChatAttachment
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

		private ChipTextPreviewViewModel? _preview;

		/// <inheritdoc/>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public override object? Preview => _preview ??= new ChipTextPreviewViewModel(this);

		/// <summary>
		/// Copies the part without the preview: the copy previews its own text, not the original's.
		/// </summary>
		public override AdditionalMessagePart Clone()
		{
			var clone = (AdditionalMessageContentPart)base.Clone();
			clone._preview = null;
			return clone;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_preview?.Dispose();
		}
	}
}
