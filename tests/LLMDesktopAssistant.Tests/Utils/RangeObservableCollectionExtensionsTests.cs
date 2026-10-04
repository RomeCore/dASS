using System.Collections.Specialized;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class RangeObservableCollectionExtensionsTests
	{
		// Keeps every returned handle alive for the duration of a test: Disposable has a finalizer that
		// would otherwise detach the subscription once the handle becomes unreachable.
		private readonly List<IDisposable> _handles = [];

		private (RangeObservableCollection<int> Source, RangeObservableCollection<int> Target) Synced(params int[] items)
		{
			var source = new RangeObservableCollection<int>(items);
			var target = new RangeObservableCollection<int>();
			_handles.Add(target.SyncFrom(source));
			return (source, target);
		}

		[Fact]
		public void InitialSync_CopiesSourceItems_ReplacingTargetContent()
		{
			var source = new RangeObservableCollection<int> { 1, 2, 3 };
			var target = new RangeObservableCollection<int> { 9, 9 };

			_handles.Add(target.SyncFrom(source));

			Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());
		}

		[Fact]
		public void InitialSync_EmptySource_ClearsTarget()
		{
			var source = new RangeObservableCollection<int>();
			var target = new RangeObservableCollection<int> { 1, 2 };

			_handles.Add(target.SyncFrom(source));

			Assert.Empty(target);
		}

		[Fact]
		public void Add_IsMirroredAtCorrectIndex()
		{
			var (source, target) = Synced(1, 3);

			source.Insert(1, 2);

			Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());
		}

		[Fact]
		public void AddRange_IsMirroredAsRange()
		{
			var (source, target) = Synced(1, 4);

			source.InsertRange(1, new[] { 2, 3 });

			Assert.Equal(new[] { 1, 2, 3, 4 }, target.ToArray());
		}

		[Fact]
		public void RemoveAt_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3);

			source.RemoveAt(1);

			Assert.Equal(new[] { 1, 3 }, target.ToArray());
		}

		[Fact]
		public void RemoveRange_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3, 4);

			source.RemoveRange(1, 2);

			Assert.Equal(new[] { 1, 4 }, target.ToArray());
		}

		[Fact]
		public void Remove_ByValue_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3);

			source.Remove(3);

			Assert.Equal(new[] { 1, 2 }, target.ToArray());
		}

		[Fact]
		public void Set_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3);

			source[1] = 20;

			Assert.Equal(new[] { 1, 20, 3 }, target.ToArray());
		}

		[Fact]
		public void ReplaceRange_IsMirrored_WithDifferentLength()
		{
			var (source, target) = Synced(1, 2, 3);

			source.ReplaceRange(1, 1, new[] { 9, 8 });

			Assert.Equal(new[] { 1, 9, 8, 3 }, target.ToArray());
		}

		[Fact]
		public void Move_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3);

			source.Move(0, 2);

			Assert.Equal(new[] { 2, 3, 1 }, target.ToArray());
		}

		[Fact]
		public void MoveRange_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3, 4);

			source.MoveRange(0, 2, 2);

			Assert.Equal(new[] { 3, 4, 1, 2 }, target.ToArray());
		}

		[Fact]
		public void Clear_IsMirrored()
		{
			var (source, target) = Synced(1, 2, 3);

			source.Clear();

			Assert.Empty(target);
		}

		[Fact]
		public void SourceReset_IsMirrored()
		{
			var (source, target) = Synced(1, 2);
			source.PreferResetForRangeOperations = true;

			source.Reset(new[] { 5, 6, 7 });

			Assert.Equal(new[] { 5, 6, 7 }, target.ToArray());
		}

		[Fact]
		public void TargetReceivesGranularEvents_NotReset()
		{
			var (source, target) = Synced();
			var actions = new List<NotifyCollectionChangedAction>();
			target.CollectionChanged += (s, e) => actions.Add(e.Action);

			source.AddRange(new[] { 1, 2, 3 });

			Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());
			var action = Assert.Single(actions);
			Assert.Equal(NotifyCollectionChangedAction.Add, action);
		}

		[Fact]
		public void Dispose_StopsSynchronization_AndFreezesTarget()
		{
			var (source, target) = Synced(1);

			_handles[^1].Dispose();
			source.Add(2);

			Assert.Equal(new[] { 1 }, target.ToArray());
		}

		[Fact]
		public void Dispose_IsIdempotent()
		{
			var (source, target) = Synced();

			_handles[^1].Dispose();
			_handles[^1].Dispose();

			source.Add(1);

			Assert.Empty(target);
		}

		[Fact]
		public void DivergedTarget_IsHealedByFullResync()
		{
			var (source, target) = Synced(1, 2, 3);
			Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());

			target.Clear(); // external mutation: the target diverges

			source.RemoveAt(0); // the point edit cannot be applied -> silent self-heal

			Assert.Equal(new[] { 2, 3 }, target.ToArray());
		}

		[Fact]
		public void ReentrantChange_IsCoalescedIntoFullResync()
		{
			var (source, target) = Synced();

			bool mutated = false;
			target.CollectionChanged += (s, e) =>
			{
				if (mutated)
					return;
				mutated = true;
				source.Add(999);
			};

			source.Add(1);

			Assert.Equal(new[] { 1, 999 }, target.ToArray());
			Assert.Equal(source.ToArray(), target.ToArray());
		}

		[Fact]
		public void SelfSync_IsNoOp()
		{
			var collection = new RangeObservableCollection<int> { 1, 2 };

			_handles.Add(collection.SyncFrom(collection));
			collection.Add(3);

			Assert.Equal(new[] { 1, 2, 3 }, collection.ToArray());
		}

		[Fact]
		public void NullArguments_Throw()
		{
			var source = new RangeObservableCollection<int>();

			Assert.Throws<ArgumentNullException>(() => RangeObservableCollectionExtensions.SyncFrom<int>(null!, source));
			Assert.Throws<ArgumentNullException>(() => source.SyncFrom(null!));
		}
	}
}
