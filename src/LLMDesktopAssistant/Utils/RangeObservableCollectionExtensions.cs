using System.Collections;
using System.Collections.Specialized;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// Extension methods that mirror one <see cref="RangeObservableCollection{T}"/> into another.
	/// </summary>
	public static class RangeObservableCollectionExtensions
	{
		/// <summary>
		/// Makes <paramref name="target"/> a live mirror of <paramref name="from"/>.
		/// <para/>
		/// On start the target is reset with the current items of the source. Afterwards the source's
		/// <see cref="INotifyCollectionChanged"/> notifications are replayed onto the target as granular
		/// point edits (insert / remove / replace / move, using the matching range operations when a
		/// notification carries several items). A source <see cref="NotifyCollectionChangedAction.Reset"/>
		/// carries no items, so it is applied as a full reset of the target from the source.
		/// <para/>
		/// The replay is performed synchronously on the thread that changes the source (no UI-thread
		/// marshalling); the target's own <see cref="RangeObservableCollection{T}.RaiseInUIThread"/> still
		/// governs the notifications the target raises itself. The collection-level settings of the source
		/// (such as <see cref="RangeObservableCollection{T}.PreferResetForRangeOperations"/>) are never copied
		/// onto the target - only the items are synchronized.
		/// <para/>
		/// If a point edit cannot be applied (for example because the target diverged after an external
		/// mutation), the target is silently restored with a full reset. A change raised by the target while
		/// it is being mutated (for example because one of its listeners touches the source) cannot be
		/// replayed pointwise anymore, so it is coalesced into a single full reset performed once the outer
		/// operation completes.
		/// <para/>
		/// The returned <see cref="IDisposable"/> stops the synchronization when disposed: the target keeps its
		/// last synchronized content and is no longer touched by later source changes. Disposing is idempotent.
		/// </summary>
		/// <typeparam name="T">The type of elements in the collections.</typeparam>
		/// <param name="target">The collection that is kept in sync with <paramref name="from"/>.</param>
		/// <param name="from">The collection that acts as the source of truth.</param>
		/// <returns>An <see cref="IDisposable"/> that stops the synchronization when disposed.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="from"/> is <see langword="null"/>.</exception>
		public static IDisposable SyncFrom<T>(this RangeObservableCollection<T> target, RangeObservableCollection<T> from)
		{
			ArgumentNullException.ThrowIfNull(target);
			ArgumentNullException.ThrowIfNull(from);

			// Syncing a collection with itself is a no-op, and would otherwise loop through its own events.
			if (ReferenceEquals(target, from))
				return Disposable.Empty;

			return new SyncSubscription<T>(target, from).Start();
		}

		private sealed class SyncSubscription<T>
		{
			private readonly RangeObservableCollection<T> _target;
			private readonly RangeObservableCollection<T> _from;
			private readonly NotifyCollectionChangedEventHandler _onSourceChanged;
			private readonly Disposable _lifetime;

			private int _applying;
			private int _dirty;
			private int _disposed;

			public SyncSubscription(RangeObservableCollection<T> target, RangeObservableCollection<T> from)
			{
				_target = target;
				_from = from;
				_onSourceChanged = OnSourceChanged;
				_lifetime = new Disposable(Detach);
			}

			public IDisposable Start()
			{
				// Subscribe before the initial reset so that a change that happens in between is not lost.
				_from.CollectionChanged += _onSourceChanged;
				RunGuarded(FullResync);

				return _lifetime;
			}

			private void Detach()
			{
				Volatile.Write(ref _disposed, 1);
				_from.CollectionChanged -= _onSourceChanged;
			}

			private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
			{
				if (Volatile.Read(ref _disposed) != 0)
					return;

				RunGuarded(() => Apply(e));
			}

			/// <summary>
			/// Runs <paramref name="apply"/> while guarding against re-entrancy. A change raised while we are
			/// already applying one cannot be replayed pointwise (the indices may have shifted), so it is
			/// flagged and coalesced into a single full resync performed after the outer operation finishes.
			/// </summary>
			private void RunGuarded(Action apply)
			{
				if (Interlocked.CompareExchange(ref _applying, 1, 0) != 0)
				{
					Volatile.Write(ref _dirty, 1);
					return;
				}

				try
				{
					apply();

					while (Volatile.Read(ref _dirty) != 0)
					{
						Volatile.Write(ref _dirty, 0);
						FullResync();
					}
				}
				finally
				{
					Volatile.Write(ref _applying, 0);
				}
			}

			private void Apply(NotifyCollectionChangedEventArgs e)
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						TryPointEdit(() => Insert(e.NewStartingIndex, e.NewItems));
						break;
					case NotifyCollectionChangedAction.Remove:
						TryPointEdit(() => Remove(e.OldStartingIndex, e.OldItems));
						break;
					case NotifyCollectionChangedAction.Replace:
						TryPointEdit(() => Replace(e.NewStartingIndex, e.OldItems, e.NewItems));
						break;
					case NotifyCollectionChangedAction.Move:
						TryPointEdit(() => Move(e.OldStartingIndex, e.NewStartingIndex, e.NewItems));
						break;
					default:
						// Reset carries no items, so the only faithful application is a full resync.
						FullResync();
						break;
				}
			}

			private void TryPointEdit(Action pointEdit)
			{
				try
				{
					pointEdit();
				}
				catch (Exception)
				{
					// The target has diverged (or one of its listeners threw): silently heal it.
					FullResync();
				}
			}

			private void Insert(int index, IList? items)
			{
				if (items == null || items.Count == 0)
					return;

				if (items.Count == 1)
					_target.Insert(index, (T)items[0]!);
				else
					_target.InsertRange(index, ToTypedList(items));
			}

			private void Remove(int index, IList? items)
			{
				if (items == null || items.Count == 0)
					return;

				if (items.Count == 1)
					_target.RemoveAt(index);
				else
					_target.RemoveRange(index, items.Count);
			}

			private void Replace(int index, IList? oldItems, IList? newItems)
			{
				int oldCount = oldItems?.Count ?? 0;
				int newCount = newItems?.Count ?? 0;

				if (newCount == 0)
				{
					Remove(index, oldItems);
					return;
				}

				if (oldCount == 1 && newCount == 1)
					_target.Set(index, (T)newItems![0]!);
				else
					_target.ReplaceRange(index, oldCount, ToTypedList(newItems!));
			}

			private void Move(int oldIndex, int newIndex, IList? items)
			{
				if (items == null || items.Count == 0)
					return;

				if (items.Count == 1)
					_target.Move(oldIndex, newIndex);
				else
					_target.MoveRange(oldIndex, items.Count, newIndex);
			}

			private void FullResync()
			{
				_target.Reset(_from);
			}

			private static List<T> ToTypedList(IList items)
			{
				var list = new List<T>(items.Count);
				foreach (var item in items)
					list.Add((T)item!);
				return list;
			}
		}
	}
}
