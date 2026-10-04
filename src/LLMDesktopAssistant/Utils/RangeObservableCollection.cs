using System.Collections;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using LiteDB;
using Serilog;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// A thread-safe ObservableCollection that notifies when a range of items is added or removed.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	public class RangeObservableCollection<T> : NotifyPropertyChanged, IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged
	{
		private readonly List<T> _items;
		private readonly object _lock;
		private volatile int _count;

		/// <summary>
		/// Gets the list that backs this collection.
		/// </summary>
		protected List<T> Items => _items;

		/// <summary>
		/// Gets the number of elements contained in the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public int Count => _count;

		/// <summary>
		/// Gets a value indicating whether the collection contains any items.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool Any => _count > 0;

		/// <summary>
		/// Gets a value indicating whether the <see cref="RangeObservableCollection{T}"/> is read-only.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsReadOnly => false;

		/// <summary>
		/// Gets a value indicating whether the <see cref="RangeObservableCollection{T}"/> has a fixed size.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsFixedSize => false;

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="RangeObservableCollection{T}"/> is synchronized (thread-safe).
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsSynchronized => true;

		/// <summary>
		/// Gets an object that can be used to synchronize access to the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public object SyncRoot => _lock;

		/// <summary>
		/// Gets or sets the element at the specified index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public T this[int index] { get => Get(index); set => Set(index, value); }
		object? IList.this[int index] { get => Get(index); set => Set(index, (T)value!); }

		/// <summary>
		/// Gets or sets a value indicating whether to use snapshot enumeration for <see cref="GetEnumerator"/> method.
		/// </summary>
		public bool UseSnapshotEnumeration { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to prefer a <see cref="NotifyCollectionChangedAction.Reset"/> for range operations.
		/// If set to false, this collection will never raise <see cref="NotifyCollectionChangedAction.Reset"/> for any operations.
		/// </summary>
		public bool PreferResetForRangeOperations { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether to raise events in the UI thread.
		/// </summary>
		public bool RaiseInUIThread { get; set; } = false;

		private NotifyCollectionChangedEventHandler? _collectionChanged;
		/// <summary>
		/// The event that is raised when the collection changes.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? CollectionChanged
		{
			add
			{
				_collectionChanged += value;
			}
			remove
			{
				_collectionChanged -= value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RangeObservableCollection{T}"/> class.
		/// </summary>
		public RangeObservableCollection()
		{
			_items = [];
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RangeObservableCollection{T}"/> class with the elements from the specified collection.
		/// </summary>
		/// <param name="collection">The collection whose elements are copied to the new <see cref="RangeObservableCollection{T}"/>.</param>
		public RangeObservableCollection(IEnumerable<T> collection)
		{
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			_items = collection.ToList();
			_count = _items.Count;
			_lock = new();
		}

		/// <summary>
		/// Raises the <see cref="CollectionChanged"/> event with the provided arguments.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			_collectionChanged?.Invoke(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> and <see cref="CollectionChanged"/> events with the provided arguments.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void RaiseChangedEvents(NotifyCollectionChangedEventArgs e)
		{
			if (RaiseInUIThread)
			{
				Dispatcher.UIThread.Invoke(() =>
				{
					OnCollectionChanged(e);
					RaisePropertyChanged(nameof(Count));
					RaisePropertyChanged(nameof(Any));
					RaisePropertyChanged("Item[]");
				});
			}
			else
			{
				OnCollectionChanged(e);
				RaisePropertyChanged(nameof(Count));
				RaisePropertyChanged(nameof(Any));
				RaisePropertyChanged("Item[]");
			}
		}

		/// <summary>
		/// Updates the count of items in the collection.
		/// </summary>
		protected void UpdateCount()
		{
			_count = _items.Count;
		}

		/// <summary>
		/// Adds an item to the end of the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		/// <param name="item">The object to add.</param>
		public virtual void Add(T item)
		{
			int startIndex;

			lock (_lock)
			{
				startIndex = _count;
				_items.Add(item);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, startIndex));
		}
		int IList.Add(object? value)
		{
			int count = _count;
			Add((T)value!);
			return count;
		}

		/// <summary>
		/// Adds a range of items to the end of the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		/// <param name="items">The collection of objects to add.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="items"/> parameter is <see langword="null"/>.</exception>
		public virtual void AddRange(IEnumerable<T> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			var itemsList = items as List<T> ?? items.ToList();
			if (itemsList.Count == 0)
				return;
			int startIndex;

			lock (_lock)
			{
				startIndex = _count;
				_items.AddRange(itemsList);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList, startIndex));
		}

		/// <summary>
		/// Inserts an item into the <see cref="RangeObservableCollection{T}"/> at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which the item should be inserted.</param>
		/// <param name="item">The object to insert.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is out of range.</exception>
		public virtual void Insert(int index, T item)
		{
			if (index < 0 || index > _count)
				throw new ArgumentOutOfRangeException(nameof(index));

			lock (_lock)
			{
				_items.Insert(index, item);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
		}
		void IList.Insert(int index, object? value)
		{
			Insert(index, (T)value!);
		}

		/// <summary>
		/// Inserts a range of items into the <see cref="RangeObservableCollection{T}"/> at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which the first item should be inserted.</param>
		/// <param name="items">The collection of objects to insert.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is out of range.</exception>
		/// <exception cref="ArgumentNullException">The specified <paramref name="items"/> parameter is <see langword="null"/>.</exception>
		public virtual void InsertRange(int index, IEnumerable<T> items)
		{
			if (index < 0 || index > _count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			var itemsList = items as List<T> ?? items.ToList();
			if (itemsList.Count == 0)
			{
				return;
			}

			lock (_lock)
			{
				_items.InsertRange(index, itemsList);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations && itemsList.Count > 1)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList, index));
		}

		/// <summary>
		/// Removes the first occurrence of a specific object from the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		/// <param name="item">The object to remove.</param>
		/// <returns><see langword="true"/> if the item was successfully removed; otherwise, <see langword="false"/>.</returns>
		public virtual bool Remove(T item)
		{
			int index;
			T removedItem;

			lock (_lock)
			{
				index = IndexOf(item);
				if (index == -1)
					return false;

				removedItem = _items[index];
				_items.RemoveAt(index);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
			return true;
		}
		void IList.Remove(object? value)
		{
			Remove((T)value!);
		}

		/// <summary>
		/// Removes the item at the specified index of the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is out of range.</exception>
		public virtual void RemoveAt(int index)
		{
			if (index < 0 || index >= _count)
				throw new ArgumentOutOfRangeException(nameof(index));

			T removedItem;

			lock (_lock)
			{
				removedItem = _items[index];
				_items.RemoveAt(index);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
		}

		/// <summary>
		/// Removes a range of items from the <see cref="RangeObservableCollection{T}"/> starting at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which the first item to remove should be located.</param>
		/// <param name="count">The number of items to remove.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> or <paramref name="count"/> is out of range.</exception>
		public virtual void RemoveRange(int index, int count)
		{
			if (index < 0 || index >= _count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0 || index + count > _count)
				throw new ArgumentOutOfRangeException(nameof(count));

			if (count == 0)
			{
				return;
			}

			List<T> removedItems;

			lock (_lock)
			{
				removedItems = _items.GetRange(index, count);
				_items.RemoveRange(index, count);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations && count > 1)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, index));
		}

		/// <summary>
		/// Replaces the items at the specified range with a new set of items.
		/// </summary>
		/// <param name="index">The zero-based index at which the first item to replace should be located.</param>
		/// <param name="count">The number of items to replace.</param>
		/// <param name="items">The collection of objects to insert.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> or <paramref name="count"/> is out of range.</exception>
		/// <exception cref="ArgumentNullException">The specified <paramref name="items"/> parameter is <see langword="null"/>.</exception>
		public virtual void ReplaceRange(int index, int count, IEnumerable<T> items)
		{
			if (index < 0 || index >= _count)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (count < 0 || index + count > _count)
				throw new ArgumentOutOfRangeException(nameof(count));

			if (items == null)
				throw new ArgumentNullException(nameof(items));

			List<T> itemsList = items as List<T> ?? items.ToList(), oldItems;
			if (itemsList.Count == 0)
			{
				RemoveRange(index, count);
				return;
			}

			lock (_lock)
			{
				oldItems = _items.GetRange(index, count);
				_items.RemoveRange(index, count);
				_items.InsertRange(index, itemsList);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations && (count > 1 || itemsList.Count > 1))
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, itemsList, oldItems, index));
		}

		/// <summary>
		/// Moves the item at the specified index to a new index.
		/// </summary>
		/// <param name="oldIndex">The zero-based index of the item to move.</param>
		/// <param name="newIndex">The zero-based index to which the item should be moved.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="oldIndex"/> or <paramref name="newIndex"/> is out of range.</exception>
		public virtual void Move(int oldIndex, int newIndex)
		{
			if (oldIndex < 0 || oldIndex >= _count)
				throw new ArgumentOutOfRangeException(nameof(oldIndex));

			if (newIndex < 0 || newIndex >= _count)
				throw new ArgumentOutOfRangeException(nameof(newIndex));

			if (newIndex == oldIndex)
				return;

			T item;

			lock (_lock)
			{
				item = _items[oldIndex];
				_items.RemoveAt(oldIndex);
				_items.Insert(newIndex, item);
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
		}

		/// <summary>
		/// Moves a range of items so that the first moved item ends up at <paramref name="newIndex"/>.
		/// </summary>
		/// <param name="oldIndex">The zero-based index at which the first item to move should be located.</param>
		/// <param name="count">The number of items to move.</param>
		/// <param name="newIndex">The zero-based index that the first moved item will occupy after the move (its final position), in the resulting collection.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="oldIndex"/>, <paramref name="count"/> or <paramref name="newIndex"/> is out of range.</exception>
		public virtual void MoveRange(int oldIndex, int count, int newIndex)
		{
			if (oldIndex < 0 || oldIndex >= _count)
				throw new ArgumentOutOfRangeException(nameof(oldIndex));
			if (count < 0 || oldIndex + count > _count)
				throw new ArgumentOutOfRangeException(nameof(count));
			if (newIndex < 0 || newIndex > _count - count)
				throw new ArgumentOutOfRangeException(nameof(newIndex));

			// Determine if move operation will take no effect
			if (count == 0 || newIndex == oldIndex)
				return;

			List<T> movedItems;

			lock (_lock)
			{
				movedItems = _items.GetRange(oldIndex, count);
				_items.RemoveRange(oldIndex, count);
				_items.InsertRange(newIndex, movedItems);
			}

			if (PreferResetForRangeOperations && count > 1)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, newIndex, oldIndex));
		}

		/// <summary>
		/// Clears all items from the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		public virtual void Clear()
		{
			if (_count == 0)
				return;

			List<T> oldItems;

			lock (_lock)
			{
				oldItems = _items.ToList();
				_items.Clear();
				_count = 0;
			}

			if (PreferResetForRangeOperations)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems, 0));
		}

		/// <summary>
		/// Clears all items from the <see cref="RangeObservableCollection{T}"/> and resets it with a new set of items.
		/// </summary>
		/// <param name="items">The collection of objects to reset the <see cref="RangeObservableCollection{T}"/> with.</param>
		/// <exception cref="ArgumentNullException">The specified <paramref name="items"/> parameter is <see langword="null"/>.</exception>
		public virtual void Reset(IEnumerable<T> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			List<T> itemsList = items as List<T> ?? items.ToList(), oldItems;
			if (itemsList.Count == 0)
			{
				Clear();
				return;
			}

			lock (_lock)
			{
				oldItems = _items.ToList();
				_items.Clear();
				_items.AddRange(items);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else if (oldItems.Count > 0)
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, itemsList, oldItems, 0));
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList, 0));
		}

		/// <summary>
		/// Gets the element at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the element to get.</param>
		/// <returns>The element at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is out of range.</exception>
		public virtual T Get(int index)
		{
			if (index < 0 || index >= _count)
				throw new ArgumentOutOfRangeException(nameof(index));

			lock (_lock)
				return _items[index];
		}

		/// <summary>
		/// Sets the element at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the element to set.</param>
		/// <param name="item">The object to set.</param>
		/// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is out of range.</exception>
		public virtual void Set(int index, T item)
		{
			if (index < 0 || index >= _count)
				throw new ArgumentOutOfRangeException(nameof(index));

			T oldItem;

			lock (_lock)
			{
				oldItem = _items[index];
				_items[index] = item;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
		}

		/// <summary>
		/// Returns the zero-based index of the first occurrence of an object in the <see cref="RangeObservableCollection{T}"/>.
		/// </summary>
		/// <param name="item">The object to locate in the <see cref="RangeObservableCollection{T}"/>. The value can be <see langword="null"/> for reference types.</param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="item"/> in the <see cref="RangeObservableCollection{T}"/>, if found; otherwise, -1.</returns>
		public virtual int IndexOf(T item)
		{
			lock (_lock)
				return _items.IndexOf(item);
		}

		/// <summary>
		/// Returns the zero-based index of the first occurrence of an object in the <see cref="RangeObservableCollection{T}"/> starting from a specified index.
		/// </summary>
		/// <param name="item">The object to locate in the <see cref="RangeObservableCollection{T}"/>. The value can be <see langword="null"/> for reference types.</param>
		/// <param name="index">The zero-based starting index of the search.</param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="item"/> in the range of elements in the <see cref="RangeObservableCollection{T}"/> starting at <paramref name="index"/>, if found; otherwise, -1.</returns>
		public virtual int IndexOf(T item, int index)
		{
			lock (_lock)
				return _items.IndexOf(item, index);
		}

		/// <summary>
		/// Returns the zero-based index of the first occurrence of an object in the <see cref="RangeObservableCollection{T}"/> starting from a specified index and for a specified number of elements.
		/// </summary>
		/// <param name="item">The object to locate in the <see cref="RangeObservableCollection{T}"/>. The value can be <see langword="null"/> for reference types.</param>
		/// <param name="index">The zero-based starting index of the search.</param>
		/// <param name="count">The count of elements in the range to search.</param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="item"/> in the range of elements in the <see cref="RangeObservableCollection{T}"/> starting at <paramref name="index"/> and containing <paramref name="count"/> elements, if found; otherwise, -1.</returns>
		public virtual int IndexOf(T item, int index, int count)
		{
			lock (_lock)
				return _items.IndexOf(item, index, count);
		}
		int IList.IndexOf(object? value)
		{
			if (value is T _value)
				return IndexOf(_value);
			return IndexOf(default!);
		}

		/// <summary>
		/// Copies the elements of the <see cref="RangeObservableCollection{T}"/> to an existing one-dimensional array, starting at a specified index.
		/// </summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from <see cref="RangeObservableCollection{T}"/>. The array must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		public virtual void CopyTo(T[] array, int arrayIndex)
		{
			lock (_lock)
				_items.CopyTo(array, arrayIndex);
		}
		void ICollection.CopyTo(Array array, int index)
		{
			Array.Copy(Items.ToArray(), 0, array, index, Count);
		}

		/// <summary>
		/// Determines whether the <see cref="RangeObservableCollection{T}"/> contains a specific value.
		/// </summary>
		/// <param name="item">The object to locate in the <see cref="RangeObservableCollection{T}"/>. The value can be <see langword="null"/> for reference types.</param>
		/// <returns><see langword="true"/> if the <see cref="RangeObservableCollection{T}"/> contains the specified value; otherwise, <see langword="false"/>./returns>
		public virtual bool Contains(T item)
		{
			lock (_lock)
				return _items.Contains(item);
		}
		bool IList.Contains(object? value)
		{
			if (value is T _value)
				return Contains(_value);
			return Contains(default!);
		}

		/// <summary>
		/// Returns a snapshot enumerator for the collection.
		/// This method is thread-safe and returns a snapshot of the current state of the collection,
		/// which can be enumerated without blocking other threads from modifying the collection.
		/// </summary>
		/// <returns>A snapshot enumerator for the collection.</returns>
		public IEnumerator<T> GetSnapshotEnumerator()
		{
			lock (_lock)
				return ((IEnumerable<T>)_items.ToArray()).GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// Based on the value of <see cref="UseSnapshotEnumeration"/>, it either returns a snapshot enumerator or the current enumerator.
		/// </summary>
		/// <returns>An enumerator that iterates through the collection.</returns>
		public IEnumerator<T> GetEnumerator()
		{
			if (UseSnapshotEnumeration)
				return GetSnapshotEnumerator();

			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	internal static class EventArgsCache
	{
		public static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
	}
}