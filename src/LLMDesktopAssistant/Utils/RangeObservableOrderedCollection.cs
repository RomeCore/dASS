using System.Collections.Specialized;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// A thread-safe ordered <see cref="RangeObservableCollection{T}"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class RangeObservableOrderedCollection<T> : RangeObservableCollection<T>
	{
#pragma warning disable CS0809

		private IComparer<T> _comparer = Comparer<T>.Default;
		/// <summary>
		/// Gets or sets the comparer used to sort the elements in the collection.
		/// </summary>
		public IComparer<T> Comparer
		{
			get => _comparer;
			set
			{
				if (SetProperty(ref _comparer, value) && Count > 0)
					Reset(this);
			}
		}

		[Obsolete("Insert method is not supported because collection should be sorted. Add items using 'Add' method only.")]
		public override void Insert(int index, T item)
		{
			Add(item);
		}

		[Obsolete("InsertRange method is not supported because collection should be sorted. Add items using 'Add' method only.")]
		public override void InsertRange(int index, IEnumerable<T> items)
		{
			AddRange(items);
		}

		[Obsolete("Move method is not supported because collection should be sorted.")]
		public override void Move(int oldIndex, int newIndex)
		{
			// Do nothing
		}

		[Obsolete("MoveRange method is not supported because collection should be sorted.")]
		public override void MoveRange(int oldIndex, int count, int newIndex)
		{
			// Do nothing
		}

		[Obsolete("ReplaceRange method is not supported because collection should be sorted. Use RemoveRange + AddRange instead.")]
		public override void ReplaceRange(int index, int count, IEnumerable<T> items)
		{
			RemoveRange(index, count);
			AddRange(items);
		}

		protected void Add(T item, out int insertIndex)
		{
			int index = Items.BinarySearch(item, Comparer);
			if (index < 0)
			{
				insertIndex = ~index;
			}
			else
			{
				insertIndex = index + 1;
				while (insertIndex < Count && Comparer.Compare(Items[insertIndex], item) == 0)
				{
					insertIndex++;
				}
			}

			Items.Insert(insertIndex, item);
			UpdateCount();
		}

		public override void Add(T item)
		{
			int insertIndex;

			lock (SyncRoot)
			{
				Add(item, out insertIndex);
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, insertIndex));
		}

		public override void AddRange(IEnumerable<T> items)
		{
			foreach (var item in items)
				Add(item);
		}

		public override void Set(int index, T item)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException(nameof(index));

			T oldItem;
			bool isSameOrder;
			int insertIndex = 0;

			lock (SyncRoot)
			{
				oldItem = Items[index];
				isSameOrder = Comparer.Compare(oldItem, item) == 0;
				if (isSameOrder)
				{
					Items[index] = item;
				}
				else
				{
					Items.RemoveAt(index);
					Add(item, out insertIndex);
				}
			}

			if (isSameOrder)
			{
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
			}
			else
			{
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItem, index));
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, insertIndex));
			}
		}

		public override void Reset(IEnumerable<T> items)
		{
			base.Reset(items.OrderBy(i => i, Comparer));
		}
	}
}