using System.Collections.Specialized;
using System.ComponentModel;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// One strip of the chat's input rail: the additional data that asked for this vertical placement,
	/// split into its left and right groups.
	/// </summary>
	/// <remarks>
	/// Membership is <see cref="AdditionalChatDataItemBase.IsEffectivelyVisible"/>, which the source collection
	/// does not watch, so the strip re-evaluates it whenever an item changes. Every strip subscribes to every
	/// rail item, not only to its own: moving an item to the other strip has to insert it there, and a strip
	/// that watched only its own members would never hear about that.
	/// </remarks>
	[ViewModelFor(typeof(ChatInputRailView))]
	public class ChatInputRailViewModel : ViewModelBase
	{
		private readonly AdditionalChatDataCollection _source;
		private readonly ChatInputRailVerticalPlacement _verticalPlacement;

		/// <summary>
		/// Gets the left group of the strip.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Left { get; }

		/// <summary>
		/// Gets the right group of the strip.
		/// </summary>
		public FilteredObservableCollection<AdditionalChatData> Right { get; }

		/// <summary>
		/// Gets a value indicating whether the strip has anything to show.
		/// </summary>
		public bool HasItems => Left.Any || Right.Any;

		public ChatInputRailViewModel(AdditionalChatDataCollection source, ChatInputRailVerticalPlacement verticalPlacement)
		{
			_source = source;
			_verticalPlacement = verticalPlacement;
			Left = new FilteredObservableCollection<AdditionalChatData>(source,
				item => IsRailItem(item, ChatInputRailHorizontalPlacement.Left));
			Right = new FilteredObservableCollection<AdditionalChatData>(source,
				item => IsRailItem(item, ChatInputRailHorizontalPlacement.Right));
			_source.CollectionChanged += OnSourceChanged;
			List<AdditionalChatData> initial = [.. source.OfType<AdditionalChatDataInputRailItem>()];
			OnSourceChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, initial));
		}

		/// <summary>
		/// The membership of a group: a rail item for this strip and this side, not hidden by the payload.
		/// </summary>
		private bool IsRailItem(AdditionalChatData item, ChatInputRailHorizontalPlacement horizontalPlacement)
			=> item is AdditionalChatDataInputRailItem rail
				&& rail.VerticalPlacement == _verticalPlacement
				&& rail.HorizontalPlacement == horizontalPlacement
				&& rail.IsEffectivelyVisible;

		private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
					if (item is AdditionalChatDataInputRailItem rail)
						rail.PropertyChanged -= OnItemPropertyChanged;

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
					if (item is AdditionalChatDataInputRailItem rail)
						rail.PropertyChanged += OnItemPropertyChanged;

			RaisePropertyChanged(nameof(HasItems));
		}

		/// <summary>
		/// Any change of an item can move it in or out of a group or between the strips.
		/// </summary>
		private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is null or nameof(AdditionalChatDataInputRailItem.IsEffectivelyVisible))
			{
				Left.Refresh();
				Right.Refresh();
				RaisePropertyChanged(nameof(HasItems));
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_source.CollectionChanged -= OnSourceChanged;

				foreach (var item in _source.OfType<AdditionalChatDataInputRailItem>())
					item.PropertyChanged -= OnItemPropertyChanged;

				Left.Dispose();
				Right.Dispose();
			}
		}
	}
}
