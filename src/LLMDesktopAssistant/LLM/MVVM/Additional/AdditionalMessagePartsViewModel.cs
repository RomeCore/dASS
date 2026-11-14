using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// A control over an <see cref="AdditionalChatDataCollection"/> that exposes only its
	/// <see cref="AdditionalMessagePart"/> descendants as a live filtered view (rendered as chips)
	/// and owns their removal.
	/// </summary>
	[ViewModelFor(typeof(AdditionalMessagePartsView))]
	public class AdditionalMessagePartsViewModel : ViewModelBase
	{
		private AdditionalChatDataCollection _source;

		/// <summary>
		/// The live view over the message parts of the source collection.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Parts
		{
			get => field;
			private set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the alignment of the parts, derived from the role of the message that owns them.
		/// Set by <see cref="AdditionalChatDataCollectionViewModel"/>.
		/// </summary>
		public MessageAlignment Alignment { get; set; } = MessageAlignment.Left;

		private bool _isEditing = false;
		/// <summary>
		/// Gets or sets a value indicating whether the parts are being edited.
		/// When enabled, every part is wrapped with a remove overlay.
		/// Set to <see langword="true"/> only by the user input.
		/// </summary>
		public bool IsEditing
		{
			get => _isEditing;
			set => SetProperty(ref _isEditing, value);
		}

		/// <summary>
		/// Gets the command that removes a part from the source collection.
		/// </summary>
		public ICommand RemoveCommand { get; }

		public AdditionalMessagePartsViewModel(AdditionalChatDataCollection source)
		{
			_source = source;
			Parts = new FilteredObservableCollection<AdditionalChatData>(source, item => item is AdditionalMessagePart);
			RemoveCommand = new RelayCommand<AdditionalChatData>(Remove);
		}

		private void Remove(AdditionalChatData? part)
		{
			if (part != null)
				_source.Remove(part);
		}

		/// <summary>
		/// Re-points the chips at another collection, keeping every subscription alive.
		/// </summary>
		/// <remarks>
		/// The chat list holds a snapshot while a chat is closed and the chat's own collection while it is open,
		/// so that a script appending a part updates the list entry the user is looking at.
		/// </remarks>
		public void SetSource(AdditionalChatDataCollection source)
		{
			if (ReferenceEquals(_source, source))
				return;

			_source = source;
			var previous = Parts;
			Parts = new FilteredObservableCollection<AdditionalChatData>(source, item => item is AdditionalMessagePart);
			previous.Dispose();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				Parts.Dispose();
		}
	}
}
