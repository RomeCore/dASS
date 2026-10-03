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
		private readonly AdditionalChatDataCollection _source;

		/// <summary>
		/// The live view over the message parts of the source collection.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Parts { get; }

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

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				Parts.Dispose();
		}
	}
}
