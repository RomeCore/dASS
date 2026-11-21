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
	/// The tab strip of the chat header and the payload of the selected tab, which covers the conversation below
	/// the header.
	/// </summary>
	/// <remarks>
	/// The strip is a view over the chat's own additional data, like the rail and the overlay host: an item is never
	/// copied, so its payload keeps its visual tree while another tab is open. Membership depends on the item's own
	/// state, which no collection watches, so every tab is subscribed to and the view is re-evaluated on any change.
	/// </remarks>
	public class ChatTabHostViewModel : ViewModelBase
	{
		private readonly IChatAttachmentHost _host;
		private readonly AdditionalChatDataCollection _source;

		/// <summary>
		/// Gets the visible tabs, ordered by the payload's order.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Tabs { get; }

		/// <summary>
		/// Gets a value indicating whether the strip has anything to show.
		/// </summary>
		public bool HasTabs => Tabs.Any;

		/// <summary>
		/// Gets the open tab, or <see langword="null"/> when the conversation is shown instead.
		/// </summary>
		public AdditionalChatDataTabItem? SelectedTab
			=> Tabs.OfType<AdditionalChatDataTabItem>().FirstOrDefault(tab => tab.IsSelected);

		/// <summary>
		/// Gets the payload of the open tab, or <see langword="null"/> when the conversation is shown instead.
		/// </summary>
		public AdditionalChatData? SelectedContent => SelectedTab?.Content;

		/// <summary>
		/// Gets a value indicating whether a payload covers the conversation.
		/// </summary>
		public bool IsContentShown => SelectedContent is not null;

		/// <summary>
		/// Gets the command that opens a tab, or closes it when it is the open one.
		/// </summary>
		public ICommand SelectCommand { get; }

		/// <summary>
		/// Gets the command that removes a tab together with its payload.
		/// </summary>
		public ICommand CloseCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatTabHostViewModel"/> class.
		/// </summary>
		/// <param name="host">The chat the tabs are attached to and removed from.</param>
		/// <param name="source">The chat's additional data, the single source of every tab.</param>
		public ChatTabHostViewModel(IChatAttachmentHost host, AdditionalChatDataCollection source)
		{
			_host = host;
			_source = source;

			Tabs = new FilteredObservableCollection<AdditionalChatData>(source, IsTab);
			Tabs.CollectionChanged += OnTabsChanged;
			_source.CollectionChanged += OnSourceChanged;

			foreach (var item in _source.OfType<AdditionalChatDataTabItem>())
				item.PropertyChanged += OnItemPropertyChanged;

			SelectCommand = new RelayCommand<AdditionalChatDataTabItem>(Select);
			CloseCommand = new RelayCommand<AdditionalChatDataTabItem>(Close);
		}

		/// <summary>
		/// The membership of the strip: a tab whose payload is visible.
		/// </summary>
		private static bool IsTab(AdditionalChatData item)
			=> item is AdditionalChatDataTabItem tab && tab.IsEffectivelyVisible;

		private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			RaisePropertyChanged(nameof(HasTabs));
			RaiseSelectionChanged();
		}

		private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
					if (item is AdditionalChatDataTabItem tab)
						tab.PropertyChanged -= OnItemPropertyChanged;

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
					if (item is AdditionalChatDataTabItem tab)
						tab.PropertyChanged += OnItemPropertyChanged;
		}

		/// <summary>
		/// Any change of a tab can move it in or out of the strip and open or close the content.
		/// </summary>
		private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is null or nameof(AdditionalChatDataTabItem.IsEffectivelyVisible))
				Tabs.Refresh();

			if (e.PropertyName is null
				or nameof(AdditionalChatDataTabItem.IsSelected)
				or nameof(AdditionalChatDataTabItem.IsEffectivelyVisible))
				RaiseSelectionChanged();
		}

		/// <summary>
		/// Opens the tab, or closes it when it is already the open one; selecting a tab closes every other one.
		/// </summary>
		private void Select(AdditionalChatDataTabItem? tab)
		{
			if (tab is null)
				return;

			if (tab.IsSelected)
			{
				tab.IsSelected = false;
				return;
			}

			foreach (var other in Tabs.OfType<AdditionalChatDataTabItem>())
				other.IsSelected = ReferenceEquals(other, tab);
		}

		/// <summary>
		/// Removes the tab and disposes its payload with it.
		/// </summary>
		private void Close(AdditionalChatDataTabItem? tab)
		{
			if (tab is null)
				return;

			tab.IsSelected = false;
			_host.RemoveAttachment(tab);
		}

		private void RaiseSelectionChanged()
		{
			RaisePropertyChanged(nameof(SelectedTab));
			RaisePropertyChanged(nameof(SelectedContent));
			RaisePropertyChanged(nameof(IsContentShown));
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_source.CollectionChanged -= OnSourceChanged;

				foreach (var item in _source.OfType<AdditionalChatDataTabItem>())
					item.PropertyChanged -= OnItemPropertyChanged;

				Tabs.CollectionChanged -= OnTabsChanged;
				Tabs.Dispose();
			}
		}
	}
}
