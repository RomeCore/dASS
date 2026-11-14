using System.Collections.Specialized;
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
		private readonly AdditionalChatDataCollection _source;
		private readonly IServiceProvider? _services;

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

		/// <param name="source">The rendered collection.</param>
		/// <param name="services">
		/// The chat's service scope, stamped onto the items that need it to rebuild live state after a reload
		/// (<see cref="IChatServicesAttachable"/>). Without it a restored widget keeps its data but loses its
		/// commands, so passing the scope is what makes the restore complete.
		/// </param>
		public AdditionalChatDataCollectionViewModel(AdditionalChatDataCollection source, IServiceProvider? services = null)
		{
			_source = source;
			_services = services;

			Parts = new AdditionalMessagePartsViewModel(source);
			Others = new FilteredObservableCollection<AdditionalChatData>(source, item => item is not AdditionalMessagePart);

			// Loading a chat replaces the whole collection in one reset, so every item is stamped here; later
			// additions (a tool call appending a widget) are stamped as they arrive.
			AttachServices(source);
			source.CollectionChanged += OnSourceChanged;
		}

		private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			// A move reorders what is already stamped, and only a reset drops the items from the event.
			if (e.Action == NotifyCollectionChangedAction.Move)
				return;

			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				AttachServices(_source);
				return;
			}

			if (e.NewItems is not null)
				AttachServices(e.NewItems.OfType<AdditionalChatData>());
		}

		/// <summary>
		/// Hands the chat's service scope to every item that asks for it. An envelope forwards it to its payload,
		/// so walking the collection is enough.
		/// </summary>
		private void AttachServices(IEnumerable<AdditionalChatData> items)
		{
			if (_services is null)
				return;

			foreach (var item in items)
				if (item is IChatServicesAttachable attachable)
					attachable.AttachChatServices(_services);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_source.CollectionChanged -= OnSourceChanged;
				Parts.Dispose();
				Others.Dispose();
			}
		}
	}
}
