using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// A live, read-only, filtered view over a source collection.
	/// <para/>
	/// The view subscribes to the source's <see cref="INotifyCollectionChanged"/> notifications and
	/// re-evaluates its <see cref="Predicate"/> over the whole source on every change (or on <see cref="Refresh"/>).
	/// The resulting membership changes are reported as granular <see cref="CollectionChanged"/> events:
	/// only single-item <see cref="NotifyCollectionChangedAction.Add"/> and <see cref="NotifyCollectionChangedAction.Remove"/>
	/// (never <see cref="NotifyCollectionChangedAction.Reset"/>, never <see cref="NotifyCollectionChangedAction.Move"/>),
	/// so the view keeps the <c>RangeObservableCollection&lt;T&gt;</c> guarantee of always providing old/new items.
	/// <para/>
	/// The view is a read-only projection: all mutators throw <see cref="InvalidOperationException"/>.
	/// The source is the single source of truth.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	public class FilteredObservableCollection<T> : NotifyPropertyChanged, IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged
	{
		private readonly IReadOnlyList<T> _sourceItems;
		private readonly INotifyCollectionChanged _sourceNcc;
		private readonly Func<T, bool> _predicate;

		private readonly List<T> _items;
		private readonly object _lock = new();
		private volatile int _count;

		private NotifyCollectionChangedEventHandler? _collectionChanged;

		/// <summary>
		/// The event that is raised when the filtered view changes.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? CollectionChanged
		{
			add => _collectionChanged += value;
			remove => _collectionChanged -= value;
		}

		/// <summary>
		/// Gets the predicate that determines the membership of this view. Immutable.
		/// </summary>
		public Func<T, bool> Predicate => _predicate;

		/// <summary>
		/// Gets or sets a value indicating whether to use snapshot enumeration for <see cref="GetEnumerator"/> method.
		/// </summary>
		public bool UseSnapshotEnumeration { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to raise <see cref="CollectionChanged"/> in the UI thread.
		/// </summary>
		public bool RaiseInUIThread { get; set; } = false;

		/// <summary>
		/// Gets the number of elements contained in the <see cref="FilteredObservableCollection{T}"/>.
		/// </summary>
		public int Count => _count;

		/// <summary>
		/// Gets a value indicating whether the collection contains any items.
		/// </summary>
		public bool Any => _count > 0;

		/// <summary>
		/// Gets a value indicating whether the <see cref="FilteredObservableCollection{T}"/> is read-only.
		/// Always <see langword="true"/>.
		/// </summary>
		public bool IsReadOnly => true;

		/// <summary>
		/// Gets a value indicating whether the <see cref="FilteredObservableCollection{T}"/> has a fixed size.
		/// Always <see langword="true"/> because the view is read-only.
		/// </summary>
		public bool IsFixedSize => true;

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="FilteredObservableCollection{T}"/> is synchronized (thread-safe).
		/// Always <see langword="true"/>.
		/// </summary>
		public bool IsSynchronized => true;

		/// <summary>
		/// Gets an object that can be used to synchronize access to the <see cref="FilteredObservableCollection{T}"/>.
		/// </summary>
		public object SyncRoot => _lock;

		/// <summary>
		/// Gets the element at the specified index. The setter throws because the view is read-only.
		/// </summary>
		public T this[int index] { get => Get(index); set => ThrowReadOnly(); }
		object? IList.this[int index] { get => Get(index); set => ThrowReadOnly(); }

		/// <summary>
		/// Initializes a new instance of the <see cref="FilteredObservableCollection{T}"/> class.
		/// </summary>
		/// <param name="items">The source collection. Must implement <see cref="INotifyCollectionChanged"/>.</param>
		/// <param name="predicate">The predicate that determines which items belong to the view.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException">Thrown when <paramref name="items"/> does not implement <see cref="INotifyCollectionChanged"/>.</exception>
		public FilteredObservableCollection(IReadOnlyList<T> items, Func<T, bool> predicate)
		{
			ArgumentNullException.ThrowIfNull(items);
			ArgumentNullException.ThrowIfNull(predicate);

			if (items is not INotifyCollectionChanged ncc)
				throw new ArgumentException($"The source collection must implement {nameof(INotifyCollectionChanged)}.", nameof(items));

			_sourceItems = items;
			_sourceNcc = ncc;
			_predicate = predicate;
			_items = SnapshotSource();
			_count = _items.Count;

			_sourceNcc.CollectionChanged += OnSourceCollectionChanged;
		}

		/// <summary>
		/// Re-evaluates the predicate over the whole source and reports the membership changes.
		/// Use this when the predicate depends on external mutable state, or when item state changed
		/// without a collection change.
		/// </summary>
		public void Refresh()
		{
			if (Disposed)
				return;

			RecomputeAndRaise();
		}

		private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (Disposed)
				return;

			RecomputeAndRaise();
		}

		private List<T> SnapshotSource()
		{
			var result = new List<T>();
			foreach (var item in _sourceItems)
			{
				if (_predicate(item))
					result.Add(item);
			}
			return result;
		}

		private void RecomputeAndRaise()
		{
			List<NotifyCollectionChangedEventArgs> events;

			lock (_lock)
			{
				var target = SnapshotSource();
				events = Diff(_items, target);
				_count = _items.Count;
			}

			RaiseChangedEvents(events);
		}

		/// <summary>
		/// Transforms <paramref name="current"/> in place so that it becomes equal to <paramref name="target"/>,
		/// returning the granular operations that describe the transition.
		/// Removal operations are produced in descending index order, insertions in ascending order.
		/// </summary>
		private static List<NotifyCollectionChangedEventArgs> Diff(List<T> current, List<T> target)
		{
			var events = new List<NotifyCollectionChangedEventArgs>();
			var cmp = EqualityComparer<T>.Default;

			int i = 0, j = 0;
			while (i < current.Count && j < target.Count)
			{
				if (cmp.Equals(current[i], target[j]))
				{
					i++;
					j++;
					continue;
				}

				int jLook = IndexOfFrom(target, current[i], j + 1, cmp);
				int iLook = IndexOfFrom(current, target[j], i + 1, cmp);

				if (jLook < 0 && iLook < 0)
				{
					// Substitution: the element at this position leaves and another enters.
					var removed = current[i];
					current.RemoveAt(i);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, i));

					current.Insert(i, target[j]);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, target[j], i));
					i++;
					j++;
				}
				else if (jLook < 0)
				{
					// current[i] is gone from the target.
					var removed = current[i];
					current.RemoveAt(i);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, i));
				}
				else if (iLook < 0)
				{
					// target[j] is new.
					current.Insert(i, target[j]);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, target[j], i));
					i++;
					j++;
				}
				else if (jLook - j <= iLook - i)
				{
					// The next target element appears sooner: insert it.
					current.Insert(i, target[j]);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, target[j], i));
					i++;
					j++;
				}
				else
				{
					// The current element appears later in the target: remove it.
					var removed = current[i];
					current.RemoveAt(i);
					events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, i));
				}
			}

			while (i < current.Count)
			{
				var removed = current[i];
				current.RemoveAt(i);
				events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, i));
			}

			while (j < target.Count)
			{
				current.Insert(i, target[j]);
				events.Add(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, target[j], i));
				i++;
				j++;
			}

			return events;
		}

		private static int IndexOfFrom(List<T> list, T item, int start, IEqualityComparer<T> cmp)
		{
			for (int k = start; k < list.Count; k++)
			{
				if (cmp.Equals(list[k], item))
					return k;
			}
			return -1;
		}

		private void RaiseChangedEvents(IReadOnlyList<NotifyCollectionChangedEventArgs> events)
		{
			if (events.Count == 0)
				return;

			void Raise()
			{
				for (int k = 0; k < events.Count; k++)
					_collectionChanged?.Invoke(this, events[k]);

				RaisePropertyChanged(nameof(Count));
				RaisePropertyChanged(nameof(Any));
				RaisePropertyChanged("Item[]");
			}

			if (RaiseInUIThread)
				Dispatcher.UIThread.Invoke(Raise);
			else
				Raise();
		}

		private T Get(int index)
		{
			lock (_lock)
			{
				if (index < 0 || index >= _items.Count)
					throw new ArgumentOutOfRangeException(nameof(index));
				return _items[index];
			}
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_sourceNcc.CollectionChanged -= OnSourceCollectionChanged;

			base.Dispose(disposing);
		}

		[DoesNotReturn]
		private static void ThrowReadOnly()
			=> throw new InvalidOperationException("Cannot modify the collection because it is read-only.");

		/// <summary>
		/// Returns a snapshot enumerator for the collection.
		/// This method is thread-safe and returns a snapshot of the current state of the collection,
		/// which can be enumerated without blocking other threads from modifying the collection.
		/// </summary>
		public IEnumerator<T> GetSnapshotEnumerator()
		{
			lock (_lock)
				return ((IEnumerable<T>)_items.ToArray()).GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// Based on the value of <see cref="UseSnapshotEnumeration"/>, it either returns a snapshot enumerator or the current enumerator.
		/// </summary>
		public IEnumerator<T> GetEnumerator()
		{
			if (UseSnapshotEnumeration)
				return GetSnapshotEnumerator();

			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public bool Contains(T item)
		{
			lock (_lock)
				return _items.Contains(item);
		}

		bool IList.Contains(object? value)
			=> value is T t && Contains(t);

		public int IndexOf(T item)
		{
			lock (_lock)
				return _items.IndexOf(item);
		}

		int IList.IndexOf(object? value)
			=> value is T t ? IndexOf(t) : -1;

		public void CopyTo(T[] array, int arrayIndex)
		{
			lock (_lock)
				_items.CopyTo(array, arrayIndex);
		}

		void ICollection.CopyTo(Array array, int index)
		{
			lock (_lock)
				Array.Copy(_items.ToArray(), 0, array, index, _count);
		}

		void ICollection<T>.Add(T item) => ThrowReadOnly();
		int IList.Add(object? value) => throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		void IList<T>.Insert(int index, T item) => ThrowReadOnly();
		void IList.Insert(int index, object? value) => ThrowReadOnly();
		bool ICollection<T>.Remove(T item) => throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		void IList.Remove(object? value) => ThrowReadOnly();
		void IList<T>.RemoveAt(int index) => ThrowReadOnly();
		void IList.RemoveAt(int index) => ThrowReadOnly();
		void ICollection<T>.Clear() => ThrowReadOnly();
		void IList.Clear() => ThrowReadOnly();
	}

	/// <summary>
	/// Extension methods for composing observable views.
	/// </summary>
	public static class FilteredObservableCollectionExtensions
	{
		/// <summary>
		/// Creates a live, read-only, filtered view over <paramref name="items"/>.
		/// The source must implement <see cref="INotifyCollectionChanged"/>.
		/// </summary>
		public static FilteredObservableCollection<T> Filter<T>(this IReadOnlyList<T> items, Func<T, bool> predicate)
			=> new FilteredObservableCollection<T>(items, predicate);
	}
}
