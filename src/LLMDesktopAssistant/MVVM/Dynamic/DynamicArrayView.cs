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
	/// Avalonia's <c>ItemsSourceView.SetSource</c> rejects any source that implements
	/// <see cref="INotifyCollectionChanged"/> without also implementing <see cref="IList"/>
	/// ("Collection implements INotifyCollectionChanged but not IList."), so this view exposes a
	/// read-only <see cref="IList"/> adapter to remain a valid, live <c>ItemsControl.ItemsSource</c>.
	/// All mutating members throw; the array part is changed through the owning
	/// <see cref="StructuredDynamicViewModel"/>.
	/// </remarks>
	public sealed class DynamicArrayView : IReadOnlyList<object?>, IList, INotifyCollectionChanged
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

		#region IList (read-only adapter)

		// Avalonia's ItemCollection.SetItemsSource -> ItemsSourceView.SetSource rejects any source
		// that implements INotifyCollectionChanged without also implementing IList
		// ("Collection implements INotifyCollectionChanged but not IList."). To remain a valid,
		// live ItemsControl.ItemsSource, the view therefore presents itself as a read-only IList.
		// The writes throw: the array part is mutated through the owning StructuredDynamicViewModel.

		bool IList.IsFixedSize => false;

		bool IList.IsReadOnly => true;

		bool ICollection.IsSynchronized => false;

		object ICollection.SyncRoot => this;

		object? IList.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException("Collection is read-only.");
		}

		int IList.Add(object? value) => throw new NotSupportedException("Collection is read-only.");

		void IList.Clear() => throw new NotSupportedException("Collection is read-only.");

		bool IList.Contains(object? value)
		{
			var count = Count;
			for (var i = 0; i < count; i++)
			{
				if (Equals(this[i], value))
					return true;
			}

			return false;
		}

		int IList.IndexOf(object? value)
		{
			var count = Count;
			for (var i = 0; i < count; i++)
			{
				if (Equals(this[i], value))
					return i;
			}

			return -1;
		}

		void IList.Insert(int index, object? value) => throw new NotSupportedException("Collection is read-only.");

		void IList.Remove(object? value) => throw new NotSupportedException("Collection is read-only.");

		void IList.RemoveAt(int index) => throw new NotSupportedException("Collection is read-only.");

		void ICollection.CopyTo(Array array, int index)
		{
			ArgumentNullException.ThrowIfNull(array);

			var count = Count;
			for (var i = 0; i < count; i++)
				array.SetValue(this[i], index + i);
		}

		#endregion
	}
}
