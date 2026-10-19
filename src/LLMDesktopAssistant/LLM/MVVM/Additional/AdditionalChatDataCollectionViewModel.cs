using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// A section router over an <see cref="AdditionalChatDataCollection"/>:
	/// <see cref="Parts"/> renders <see cref="AdditionalMessagePart"/> descendants as chips (WrapPanel),
	/// while <see cref="Others"/> exposes the rest as a plain filtered view (vertical stack).
	/// </summary>
	[ViewModelFor(typeof(AdditionalChatDataCollectionView))]
	public class AdditionalChatDataCollectionViewModel : ViewModelBase
	{
		/// <summary>
		/// The message-parts section (chips + remove overlays).
		/// </summary>
		public AdditionalMessagePartsViewModel Parts { get; }

		/// <summary>
		/// All additional data that is not a <see cref="AdditionalMessagePart"/>, rendered directly.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Others { get; }

		private MessageAlignment _alignment = MessageAlignment.Left;
		/// <summary>
		/// Gets the alignment of the parts, derived from the role of the owner (the message or the tool call).
		/// Set by the owner; forwarded to <see cref="Parts"/>. The rest of the data is left untouched.
		/// </summary>
		public MessageAlignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				Parts.Alignment = value;
			}
		}

		public AdditionalChatDataCollectionViewModel(AdditionalChatDataCollection source)
		{
			Parts = new AdditionalMessagePartsViewModel(source);
			Others = new FilteredObservableCollection<AdditionalChatData>(source, item => item is not AdditionalMessagePart);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Parts.Dispose();
				Others.Dispose();
			}
		}
	}
}
