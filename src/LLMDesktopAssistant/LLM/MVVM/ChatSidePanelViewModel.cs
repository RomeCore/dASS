using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The chat's fallback side panel: every visible <see cref="AdditionalChatData"/> that does not bring
	/// its own surface, shown next to the conversation.
	/// </summary>
	/// <remarks>
	/// The panel is visible only while it has something to show. Its width is the width of the grid column
	/// it lives in, bound two-way so the splitter writes back here; an empty panel drops the width to zero
	/// and keeps the last chosen value for the next time it appears.
	/// </remarks>
	public class ChatSidePanelViewModel : ViewModelBase
	{
		private readonly AdditionalChatDataCollection _source;
		private GridLength _lastVisibleWidth = new(320);

		/// <summary>
		/// The live, filtered view over the chat's additional data.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Items { get; }

		/// <summary>
		/// Gets a value indicating whether the panel has anything to show.
		/// </summary>
		public bool HasItems => Items.Any;

		/// <summary>
		/// Gets the width of the splitter column: the splitter exists only while the panel has content.
		/// </summary>
		public GridLength SplitterWidth => HasItems ? GridLength.Auto : new GridLength(0);

		/// <summary>
		/// Gets or sets the width of the panel column. Zero while the panel is empty.
		/// </summary>
		public GridLength PanelWidth
		{
			get => field;
			set
			{
				if (HasItems && value.Value > 0)
					_lastVisibleWidth = value;

				SetProperty(ref field, HasItems ? value : new GridLength(0));
			}
		}

		public ChatSidePanelViewModel(AdditionalChatDataCollection source)
		{
			_source = source;
			Items = new FilteredObservableCollection<AdditionalChatData>(source, IsPanelItem);
			Items.CollectionChanged += OnItemsChanged;
			OnItemsChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, Items));
		}

		/// <summary>
		/// The panel takes visible items that render nowhere else.
		/// </summary>
		private static bool IsPanelItem(AdditionalChatData item)
			=> item.IsVisible && item is not AdditionalChatDataItemBase;

		private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
					if (item is AdditionalChatData data)
						data.PropertyChanged -= OnItemPropertyChanged;

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
					if (item is AdditionalChatData data)
						data.PropertyChanged += OnItemPropertyChanged;

			ApplyVisibility();
		}

		/// <summary>
		/// Membership depends on <see cref="AdditionalChatData.IsVisible"/>, which the source does not watch.
		/// </summary>
		private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is null or nameof(AdditionalChatData.IsVisible))
				Items.Refresh();
		}

		/// <summary>
		/// Collapses the panel to zero width and restores the last width when content comes back.
		/// </summary>
		private void ApplyVisibility()
		{
			PanelWidth = HasItems ? _lastVisibleWidth : new GridLength(0);
			RaisePropertyChanged(nameof(HasItems));
			RaisePropertyChanged(nameof(SplitterWidth));
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				OnItemsChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Items));
				Items.CollectionChanged -= OnItemsChanged;
				Items.Dispose();
			}
		}
	}
}
