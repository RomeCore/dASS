using System.Collections;
using System.Collections.Specialized;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// A read-only, live view over the array part of a structured value. Items are materialized on
	/// demand and the view raises a reset whenever the owning view model reports a change, which
	/// makes it suitable as <c>ItemsControl.ItemsSource</c>.
	/// </summary>
	/// <remarks>
	/// This is deliberately not a full <see cref="IList"/>: the owning
	/// <see cref="StructuredDynamicViewModel"/> already implements <see cref="INotifyCollectionChanged"/>
	/// for indexer-refresh purposes, and overloading a single object with both meanings would make
	/// every member write look like a collection reset to a bound items control.
	/// </remarks>
	public sealed class DynamicArrayView : IReadOnlyList<object?>, INotifyCollectionChanged
	{
		private readonly Func<int> _countProvider;
		private readonly Func<int, object?> _itemProvider;

		internal DynamicArrayView(Func<int> countProvider, Func<int, object?> itemProvider)
		{
			_countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));
			_itemProvider = itemProvider ?? throw new ArgumentNullException(nameof(itemProvider));
		}

		/// <inheritdoc/>
		public int Count => _countProvider();

		/// <inheritdoc/>
		public object? this[int index] => _itemProvider(index);

		/// <inheritdoc/>
		public IEnumerator<object?> GetEnumerator()
		{
			var count = Count;
			for (var i = 0; i < count; i++)
				yield return _itemProvider(i);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc/>
		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		internal void RaiseReset()
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}
}
