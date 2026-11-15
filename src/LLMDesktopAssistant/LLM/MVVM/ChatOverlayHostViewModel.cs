using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The layer of overlay windows over a chat's message sequence, plus the tray those windows are restored from.
	/// </summary>
	/// <remarks>
	/// Both lists are views over the chat's own additional data, so an item is never copied into the host: the
	/// windows list keeps every visible overlay window - a hidden one is only not drawn, so its payload is not
	/// rebuilt when it comes back from the tray - while the tray lists the ones the user put away. Membership
	/// depends on the item's own state, which no collection watches, so every overlay item is subscribed to and both
	/// views are re-evaluated on any change: a window moving into the tray leaves one and enters the other.
	/// </remarks>
	[ViewModelFor(typeof(ChatOverlayHostView))]
	public class ChatOverlayHostViewModel : ViewModelBase
	{
		/// <summary>
		/// The share of the messages area one window is offset from the previous default placed one.
		/// </summary>
		private const double cascadeStep = 0.02;

		/// <summary>
		/// The default offset of the first window that arrives without geometry.
		/// </summary>
		private const double cascadeOrigin = 0.1;

		private readonly AdditionalChatDataCollection _source;
		private readonly IChatAttachmentHost _host;
		private bool _normalizingGeometry;

		/// <summary>
		/// Gets every overlay window of the chat, hidden ones included: the view hides those, so the payload keeps
		/// its visual tree while it sits in the tray.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Windows { get; }

		/// <summary>
		/// Gets the windows the user put into the tray.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Tray { get; }

		/// <summary>
		/// Gets a value indicating whether the tray has anything in it.
		/// </summary>
		public bool HasTrayItems => Tray.Any;

		/// <summary>
		/// Gets the number of windows in the tray.
		/// </summary>
		public int TrayCount => Tray.Count;

		/// <summary>
		/// Gets the command that collapses a window in place and expands it back.
		/// </summary>
		public ICommand ToggleCommand { get; }

		/// <summary>
		/// Gets the command that puts a window into the tray.
		/// </summary>
		public ICommand HideCommand { get; }

		/// <summary>
		/// Gets the command that brings a window back from the tray.
		/// </summary>
		public ICommand RestoreCommand { get; }

		/// <summary>
		/// Gets the command that destroys a window together with its payload.
		/// </summary>
		public ICommand CloseCommand { get; }

		/// <summary>
		/// Gets the command that takes away the explicit size of a window, so its content decides the size again.
		/// </summary>
		public ICommand ResetSizeCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatOverlayHostViewModel"/> class.
		/// </summary>
		/// <param name="host">The chat the windows are attached to, and disposed by.</param>
		/// <param name="source">The chat's additional data, the single source of every window.</param>
		public ChatOverlayHostViewModel(IChatAttachmentHost host, AdditionalChatDataCollection source)
		{
			_host = host;
			_source = source;

			Windows = new FilteredObservableCollection<AdditionalChatData>(source,
				item => item is AdditionalChatDataOverlayItem overlay && overlay.IsEffectivelyVisible);
			Tray = new FilteredObservableCollection<AdditionalChatData>(source,
				item => item is AdditionalChatDataOverlayItem overlay && overlay.IsInTray);

			ToggleCommand = new RelayCommand<AdditionalChatDataOverlayItem>(Toggle);
			HideCommand = new RelayCommand<AdditionalChatDataOverlayItem>(Hide);
			RestoreCommand = new RelayCommand<AdditionalChatDataOverlayItem>(Restore);
			CloseCommand = new RelayCommand<AdditionalChatDataOverlayItem>(Close);
			ResetSizeCommand = new RelayCommand<AdditionalChatDataOverlayItem>(ResetSize);

			_source.CollectionChanged += OnSourceChanged;

			foreach (var item in _source.OfType<AdditionalChatDataOverlayItem>())
				item.PropertyChanged += OnItemPropertyChanged;

			PlaceNewItems();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_source.CollectionChanged -= OnSourceChanged;

				foreach (var item in _source.OfType<AdditionalChatDataOverlayItem>())
					item.PropertyChanged -= OnItemPropertyChanged;

				Windows.Dispose();
				Tray.Dispose();
			}
		}

		/// <summary>
		/// Raises a window above its neighbours, so the one the user just touched is the one on top.
		/// </summary>
		/// <param name="item">The window that was used.</param>
		public void BringToFront(AdditionalChatDataOverlayItem item)
		{
			int neighbour = _source
				.OfType<AdditionalChatDataOverlayItem>()
				.Where(overlay => !ReferenceEquals(overlay, item))
				.Select(overlay => overlay.Z)
				.DefaultIfEmpty(0)
				.Max();

			// Already above every neighbour: a click must not persist an order that did not change.
			if (item.Z > neighbour)
				return;

			item.Z = neighbour + 1;
		}

		private void Toggle(AdditionalChatDataOverlayItem? item)
		{
			if (item is null)
				return;

			item.State = item.State == AdditionalChatDataOverlayState.Collapsed
				? AdditionalChatDataOverlayState.Normal
				: AdditionalChatDataOverlayState.Collapsed;

			if (item.State == AdditionalChatDataOverlayState.Normal)
				BringToFront(item);
		}

		private void Hide(AdditionalChatDataOverlayItem? item)
		{
			if (item is not null)
				item.State = AdditionalChatDataOverlayState.Hidden;
		}

		private void Restore(AdditionalChatDataOverlayItem? item)
		{
			if (item is null)
				return;

			item.State = AdditionalChatDataOverlayState.Normal;
			BringToFront(item);
		}

		/// <summary>
		/// Drops the size the user dragged or the script asked for, so the window hugs its payload again.
		/// </summary>
		private static void ResetSize(AdditionalChatDataOverlayItem? item)
		{
			if (item is null)
				return;

			item.Width = null;
			item.Height = null;
		}

		/// <summary>
		/// Removes the window from the chat, which disposes its payload: a live terminal goes away with its process.
		/// </summary>
		private void Close(AdditionalChatDataOverlayItem? item)
		{
			if (item is not null)
				_host.RemoveAttachment(item);
		}

		private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
					if (item is AdditionalChatDataOverlayItem overlay)
						overlay.PropertyChanged -= OnItemPropertyChanged;

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
					if (item is AdditionalChatDataOverlayItem overlay)
						overlay.PropertyChanged += OnItemPropertyChanged;

			PlaceNewItems();
		}

		/// <summary>
		/// The state and the visibility of any window decide its membership in both views.
		/// </summary>
		private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (_normalizingGeometry)
				return;

			Windows.Refresh();
			Tray.Refresh();
			RaisePropertyChanged(nameof(HasTrayItems));
			RaisePropertyChanged(nameof(TrayCount));

			PlaceNewItems();
		}

		/// <summary>
		/// Gives a newly arrived window what it cannot have asked for itself: a place in the cascade, so two of them
		/// never land on each other, and a stacking order taken from arrival.
		/// </summary>
		private void PlaceNewItems()
		{
			_normalizingGeometry = true;

			try
			{
				int index = 0;
				int top = _source
					.OfType<AdditionalChatDataOverlayItem>()
					.Select(overlay => overlay.Z)
					.DefaultIfEmpty(0)
					.Max();

				foreach (var item in Windows.OfType<AdditionalChatDataOverlayItem>())
				{
					item.Left ??= Math.Clamp(cascadeOrigin + (cascadeStep * index), 0, 1);
					item.Top ??= Math.Clamp(cascadeOrigin + (cascadeStep * index), 0, 1);

					// Zero is the value a window saved before the stacking order existed comes back with.
					if (item.Z == 0)
						item.Z = ++top;

					index++;
				}
			}
			finally
			{
				_normalizingGeometry = false;
			}
		}
	}
}
