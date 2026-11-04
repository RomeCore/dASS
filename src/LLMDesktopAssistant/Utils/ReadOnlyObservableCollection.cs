using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LLMDesktopAssistant.Utils
{
	public class ReadOnlyObservableCollection<T> : IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		private readonly IReadOnlyList<T>? _items;
		private readonly INotifyCollectionChanged? _ncc;
		private readonly INotifyPropertyChanged? _npc;

		public bool IsReadOnly => true;

		/// <summary>
		/// Gets a value indicating whether the underlying collection raises <see cref="CollectionChanged"/>. A view over
		/// a plain list does not, and subscribing to it throws; callers that subscribe must check this first.
		/// </summary>
		public bool CanRaiseCollectionChanged => _ncc is not null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private IReadOnlyList<T> GetItems()
		{
			if (_items is null)
				throw new InvalidOperationException($"Cannot access items because the collection is not initialized with IReadOnlyList<{typeof(T)}>.");
			return _items;
		}

		public T this[int index] => GetItems()[index];

		T IList<T>.this[int index] { get => GetItems()[index]; set => throw new InvalidOperationException("Cannot modify the collection because it is read-only."); }

		object? IList.this[int index] { get => GetItems()[index]; set => throw new InvalidOperationException("Cannot modify the collection because it is read-only."); }

		public int Count => GetItems().Count;

		public bool IsFixedSize => (_items as IList)?.IsFixedSize ?? false;

		public bool IsSynchronized => (_items as ICollection)?.IsSynchronized ?? false;

		public object SyncRoot => (_items as ICollection)?.SyncRoot ?? this;

		public event NotifyCollectionChangedEventHandler? CollectionChanged
		{
			add
			{
				if (_ncc is null)
					throw new InvalidOperationException("Cannot add CollectionChanged event because the collection is not initialized with INotifyCollectionChanged.");
				_ncc.CollectionChanged += value;
			}
			remove
			{
				if (_ncc is null)
					throw new InvalidOperationException("Cannot remove CollectionChanged event because the collection is not initialized with INotifyCollectionChanged.");
				_ncc.CollectionChanged -= value;
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged
		{
			add
			{
				if (_npc is null)
					throw new InvalidOperationException("Cannot add PropertyChanged event because the collection is not initialized with INotifyPropertyChanged.");
				_npc.PropertyChanged += value;
			}

			remove
			{
				if (_npc is null)
					throw new InvalidOperationException("Cannot remove PropertyChanged event because the collection is not initialized with INotifyPropertyChanged.");
				_npc.PropertyChanged -= value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ReadOnlyObservableCollection{T}"/> class.
		/// </summary>
		/// <param name="items">
		/// The list of items to wrap. Can be <see cref="IReadOnlyList{T}"/>,
		/// <see cref="INotifyCollectionChanged"/>, or <see cref="INotifyPropertyChanged"/>
		/// to provide read-only functionality of each type.
		/// </param>
		public ReadOnlyObservableCollection(object items)
		{
			_items = items as IReadOnlyList<T>;
			_ncc = items as INotifyCollectionChanged;
			_npc = items as INotifyPropertyChanged;
		}

		public IEnumerator<T> GetEnumerator() => GetItems().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetItems().GetEnumerator();

		void ICollection<T>.Add(T item)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		int IList.Add(object? value)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList<T>.Insert(int index, T item)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList.Insert(int index, object? value)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList.Remove(object? value)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList<T>.RemoveAt(int index)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList.RemoveAt(int index)
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void ICollection<T>.Clear()
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		void IList.Clear()
		{
			throw new InvalidOperationException("Cannot modify the collection because it is read-only.");
		}

		public bool Contains(T item) => GetItems().Contains(item);

		bool IList.Contains(object? value) => GetItems().Contains((T)value!);

		public int IndexOf(T item) => (GetItems() as IList<T>)?.IndexOf(item) ?? -1;

		int IList.IndexOf(object? value) => (GetItems() as IList)?.IndexOf(value!) ?? -1;

		void ICollection<T>.CopyTo(T[] array, int arrayIndex) => (GetItems() as ICollection<T>)?.CopyTo(array, arrayIndex);

		void ICollection.CopyTo(Array array, int index) => (GetItems() as ICollection)?.CopyTo(array, index);
	}
}